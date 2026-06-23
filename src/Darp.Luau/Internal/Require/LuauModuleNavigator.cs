using System.Diagnostics;
using Darp.Luau.Native;

namespace Darp.Luau.Internal.Require;

/// <summary> Provides a stateful navigation for a require-by-string module </summary>
/// <param name="virtualFileSystem">A virtual file system for abstract file operations</param>
/// <seealso href="https://github.com/luau-lang/luau/blob/master/CLI/src/VfsNavigator.cpp"/>
internal sealed class LuauModuleNavigator(ILuauFileSystem virtualFileSystem)
{
    private readonly ILuauFileSystem _virtualFileSystem = virtualFileSystem;
    private static readonly string[] s_suffixes = [".luau", ".lua"];
    private static readonly string[] s_initSuffixes = ["/init.luau", "/init.lua"];
    private const string ConfigName = ".luaurc";
    private const string LuauConfigName = ".config.luau";

    private string _realPath = string.Empty; // realPath
    private string _absoluteRealPath = string.Empty; // absoluteRealPath
    private string _modulePath = string.Empty; // modulePath
    private string _absoluteModulePath = string.Empty; // absoluteModulePath

    private string _absolutePathPrefix = string.Empty; // absolutePathPrefix

    public string RealPath => _realPath;
    public string AbsoluteRealPath => _absoluteRealPath;

    public static string NormalizePath(string path) => FileUtils.NormalizePath(path);

    public luarequire_NavigateResult ResetToStdIn()
    {
        string currentWorkingDirectory = _virtualFileSystem.GetCurrentDirectory();

        _realPath = "./stdin";
        _absoluteRealPath = FileUtils.NormalizePath($"{currentWorkingDirectory}/stdin");
        _modulePath = "./stdin";
        _absoluteModulePath = GetModulePath(_absoluteRealPath);

        int nPosFirstSlash = FileUtils.RequiredIndexOfFirstSlash(_absoluteRealPath);
        _absolutePathPrefix = _absoluteRealPath[..nPosFirstSlash];

        return luarequire_NavigateResult.NAVIGATE_SUCCESS;
    }

    public luarequire_NavigateResult ResetToPath(string path)
    {
        string normalizedPath = FileUtils.NormalizePath(path);

        if (FileUtils.IsAbsolutePath(normalizedPath))
        {
            _absoluteModulePath = _modulePath = GetModulePath(normalizedPath);

            int nPosFirstSlash = FileUtils.RequiredIndexOfFirstSlash(normalizedPath);
            _absolutePathPrefix = normalizedPath[..nPosFirstSlash];
        }
        else
        {
            string currentWorkingDirectory = _virtualFileSystem.GetCurrentDirectory();
            _modulePath = GetModulePath(normalizedPath);
            string strJoinedPath = FileUtils.NormalizePath($"{currentWorkingDirectory}/{normalizedPath}");
            _absoluteModulePath = GetModulePath(strJoinedPath);

            int nPosFirstSlash = FileUtils.RequiredIndexOfFirstSlash(strJoinedPath);
            _absolutePathPrefix = strJoinedPath[..nPosFirstSlash];
        }

        return UpdateRealPaths();
    }

    public luarequire_NavigateResult ToParent()
    {
        if (_absoluteModulePath.Equals("/", StringComparison.Ordinal))
            return luarequire_NavigateResult.NAVIGATE_NOT_FOUND;

        int nNumSlashes = _absoluteModulePath.Count(c => c == '/');
        if (nNumSlashes <= 0)
            throw new UnreachableException("No slashes found. This should not happen with a valid absolute path.");
        if (nNumSlashes == 1)
            return luarequire_NavigateResult.NAVIGATE_NOT_FOUND;

        _modulePath = FileUtils.NormalizePath($"{_modulePath}/..");
        _absoluteModulePath = FileUtils.NormalizePath($"{_absoluteModulePath}/..");

        // There is no ambiguity when navigating up in a tree.
        luarequire_NavigateResult eResult = UpdateRealPaths();
        if (eResult == luarequire_NavigateResult.NAVIGATE_AMBIGUOUS)
            eResult = luarequire_NavigateResult.NAVIGATE_SUCCESS;
        return eResult;
    }

    public luarequire_NavigateResult ToChild(string strName)
    {
        if (strName.Equals(".config", StringComparison.Ordinal))
            return luarequire_NavigateResult.NAVIGATE_NOT_FOUND;

        _modulePath = FileUtils.NormalizePath($"{_modulePath}/{strName}");
        _absoluteModulePath = FileUtils.NormalizePath($"{_absoluteModulePath}/{strName}");
        return UpdateRealPaths();
    }

    public luarequire_ConfigStatus GetConfigStatus()
    {
        bool bConfig = _virtualFileSystem.FileExists(GetConfigPath(ConfigName));
        bool bLuauConfig = _virtualFileSystem.FileExists(GetConfigPath(LuauConfigName));

        if (bConfig && bLuauConfig)
            return luarequire_ConfigStatus.CONFIG_AMBIGUOUS;
        if (bLuauConfig)
            return luarequire_ConfigStatus.CONFIG_PRESENT_LUAU;
        if (bConfig)
            return luarequire_ConfigStatus.CONFIG_PRESENT_JSON;

        return luarequire_ConfigStatus.CONFIG_ABSENT;
    }

    public string? GetConfig()
    {
        luarequire_ConfigStatus eStatus = GetConfigStatus();
        return eStatus switch
        {
            luarequire_ConfigStatus.CONFIG_PRESENT_JSON => _virtualFileSystem.ReadFile(GetConfigPath(ConfigName)),
            luarequire_ConfigStatus.CONFIG_PRESENT_LUAU => _virtualFileSystem.ReadFile(GetConfigPath(LuauConfigName)),
            _ => null,
        };
    }

    private luarequire_NavigateResult UpdateRealPaths()
    {
        ResolvedRealPath resolved = GetRealPath(_modulePath);
        if (resolved.Result != luarequire_NavigateResult.NAVIGATE_SUCCESS)
            return resolved.Result;
        ResolvedRealPath absoluteResolved = GetRealPath(_absoluteModulePath);
        if (absoluteResolved.Result != luarequire_NavigateResult.NAVIGATE_SUCCESS)
            return absoluteResolved.Result;

        _realPath = FileUtils.IsAbsolutePath(resolved.Path) ? _absolutePathPrefix + resolved.Path : resolved.Path;
        _absoluteRealPath = _absolutePathPrefix + absoluteResolved.Path;
        return luarequire_NavigateResult.NAVIGATE_SUCCESS;
    }

    private string GetConfigPath(string strFileName)
    {
        string strDirectory = _realPath;

        foreach (string strSuffix in s_initSuffixes)
        {
            if (FileUtils.HasSuffix(strDirectory, strSuffix))
            {
                strDirectory = FileUtils.RemoveSuffix(strDirectory, strSuffix);
                return $"{strDirectory}/{strFileName}";
            }
        }
        foreach (string strSuffix in s_suffixes)
        {
            if (FileUtils.HasSuffix(strDirectory, strSuffix))
            {
                strDirectory = FileUtils.RemoveSuffix(strDirectory, strSuffix);
                return $"{strDirectory}/{strFileName}";
            }
        }

        return strDirectory + "/" + strFileName;
    }

    private static string GetModulePath(string strFilePath)
    {
        strFilePath = strFilePath.Replace('\\', '/');

        if (FileUtils.IsAbsolutePath(strFilePath))
        {
            int nPosFirstSlash = FileUtils.RequiredIndexOfFirstSlash(strFilePath);
            strFilePath = strFilePath.Remove(0, nPosFirstSlash);
        }

        foreach (string strSuffix in s_initSuffixes)
        {
            if (FileUtils.HasSuffix(strFilePath, strSuffix))
                return FileUtils.RemoveSuffix(strFilePath, strSuffix);
        }
        foreach (string strSuffix in s_suffixes)
        {
            if (FileUtils.HasSuffix(strFilePath, strSuffix))
                return FileUtils.RemoveSuffix(strFilePath, strSuffix);
        }

        return strFilePath;
    }

    private ResolvedRealPath GetRealPath(string strModulePath)
    {
        int nPosLastSlash = FileUtils.RequiredIndexOfLastSlash(strModulePath);
        string strLastPart = strModulePath[(nPosLastSlash + 1)..];
        string? strSuffix = null;

        if (!strLastPart.Equals("init", StringComparison.Ordinal))
        {
            foreach (string strPotentialSuffix in s_suffixes)
            {
                if (_virtualFileSystem.FileExists(strModulePath + strPotentialSuffix))
                {
                    if (strSuffix is not null)
                        return new ResolvedRealPath(luarequire_NavigateResult.NAVIGATE_AMBIGUOUS);

                    strSuffix = strPotentialSuffix;
                }
            }
        }

        if (_virtualFileSystem.DirectoryExists(strModulePath))
        {
            if (strSuffix is not null)
                return new ResolvedRealPath(luarequire_NavigateResult.NAVIGATE_AMBIGUOUS);

            foreach (string strPotentialSuffix in s_initSuffixes)
            {
                if (_virtualFileSystem.FileExists(strModulePath + strPotentialSuffix))
                {
                    if (strSuffix is not null)
                        return new ResolvedRealPath(luarequire_NavigateResult.NAVIGATE_AMBIGUOUS);

                    strSuffix = strPotentialSuffix;
                }
            }

            // if no suffix was found yet strModulePath (without suffix) is the real path
            strSuffix ??= "";
        }

        if (strSuffix is null)
            return new ResolvedRealPath(luarequire_NavigateResult.NAVIGATE_NOT_FOUND);

        return new ResolvedRealPath(luarequire_NavigateResult.NAVIGATE_SUCCESS) { Path = strModulePath + strSuffix };
    }

    private readonly record struct ResolvedRealPath(luarequire_NavigateResult Result, string Path = "");
}
