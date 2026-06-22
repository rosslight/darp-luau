using System.Text;
using Darp.Luau.Native;

namespace Darp.Luau.Internal.Require;

internal class LuauModuleNavigator
{
    private static readonly string[] s_suffixes = [".luau", ".lua"];
    private static readonly string[] s_initSuffixes = ["/init.luau", "/init.lua"];
    private const string ConfigName = ".luaurc";
    private const string LuauConfigName = ".config.luau";

    private string _strModulePath = ""; // modulePath
    private string _strAbsoluteModulePath = ""; // absoluteModulePath
    private string _strAbsolutePathPrefix = ""; // absolutePathPrefix

    public string FilePath { get; private set; } = ""; // realPath
    public string AbsoluteFilePath { get; private set; } = ""; // absoluteRealPath

    public luarequire_NavigateResult ResetToStdIn()
    {
        FilePath = "./stdin";
        AbsoluteFilePath = NormalizePath(Directory.GetCurrentDirectory() + "/stdin");
        _strModulePath = "./stdin";
        _strAbsoluteModulePath = GetModulePath(AbsoluteFilePath);

        int nPosFirstSlash = AbsoluteFilePath.RequiredIndexOfFirstSlash();
        _strAbsolutePathPrefix = AbsoluteFilePath.Substring(0, nPosFirstSlash);

        return luarequire_NavigateResult.NAVIGATE_SUCCESS;
    }

    public luarequire_NavigateResult ResetToPath(string strPath)
    {
        strPath = NormalizePath(strPath);

        if (LuauRequireByString.IsAbsolutePath(strPath))
        {
            _strAbsoluteModulePath = _strModulePath = GetModulePath(strPath);

            int nPosFirstSlash = strPath.RequiredIndexOfFirstSlash();
            _strAbsolutePathPrefix = strPath.Substring(0, nPosFirstSlash);
        }
        else
        {
            _strModulePath = GetModulePath(strPath);
            string strJoinedPath = NormalizePath(Directory.GetCurrentDirectory() + "/" + strPath);
            _strAbsoluteModulePath = GetModulePath(strJoinedPath);

            int nPosFirstSlash = strJoinedPath.RequiredIndexOfFirstSlash();
            _strAbsolutePathPrefix = strJoinedPath.Substring(0, nPosFirstSlash);
        }

        return UpdateRealPaths();
    }

    public luarequire_NavigateResult ToParent()
    {
        if (Equals(_strAbsoluteModulePath, "/"))
            return luarequire_NavigateResult.NAVIGATE_NOT_FOUND;

        int nNumSlashes = _strAbsoluteModulePath.Count(c => c == '/');
        if (nNumSlashes <= 0)
            throw new LuaException("No slashes found");
        if (nNumSlashes == 1)
            return luarequire_NavigateResult.NAVIGATE_NOT_FOUND;

        _strModulePath = NormalizePath(_strModulePath + "/..");
        _strAbsoluteModulePath = NormalizePath(_strAbsoluteModulePath + "/..");

        // There is no ambiguity when navigating up in a tree.
        luarequire_NavigateResult eResult = UpdateRealPaths();
        if (eResult == luarequire_NavigateResult.NAVIGATE_AMBIGUOUS)
            eResult = luarequire_NavigateResult.NAVIGATE_SUCCESS;
        return eResult;
    }

    public luarequire_NavigateResult ToChild(string strName)
    {
        if (Equals(strName, ".config"))
            return luarequire_NavigateResult.NAVIGATE_NOT_FOUND;

        _strModulePath = NormalizePath(_strModulePath + "/" + strName);
        _strAbsoluteModulePath = NormalizePath(_strAbsoluteModulePath + "/" + strName);
        return UpdateRealPaths();
    }

    public luarequire_ConfigStatus GetConfigStatus()
    {
        bool bConfig = LuauRequireByString.FileExists(GetConfigPath(ConfigName));
        bool bLuauConfig = LuauRequireByString.FileExists(GetConfigPath(LuauConfigName));

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
            luarequire_ConfigStatus.CONFIG_PRESENT_JSON => LuauRequireByString.ReadFile(GetConfigPath(ConfigName)),
            luarequire_ConfigStatus.CONFIG_PRESENT_LUAU => LuauRequireByString.ReadFile(GetConfigPath(LuauConfigName)),
            _ => throw new LuaException("Invalid config state"),
        };
    }

    private luarequire_NavigateResult UpdateRealPaths()
    {
        var resolved = ResolvedRealPath.For(_strModulePath);
        if (resolved.Result != luarequire_NavigateResult.NAVIGATE_SUCCESS)
            return resolved.Result;

        var absoluteResolved = ResolvedRealPath.For(_strAbsoluteModulePath);
        if (absoluteResolved.Result != luarequire_NavigateResult.NAVIGATE_SUCCESS)
            return absoluteResolved.Result;

        FilePath = LuauRequireByString.IsAbsolutePath(resolved.Path)
            ? _strAbsolutePathPrefix + resolved.Path
            : resolved.Path;
        AbsoluteFilePath = _strAbsolutePathPrefix + absoluteResolved.Path;
        return luarequire_NavigateResult.NAVIGATE_SUCCESS;
    }

    internal static string NormalizePath(string strPath)
    {
        string[] parts = strPath.Split('/', '\\');
        bool bIsAbsolute = LuauRequireByString.IsAbsolutePath(strPath);

        //
        // 1. Normalize path components
        //

        List<string> partsNormalized = [];
        for (int i = bIsAbsolute ? 1 : 0; i < parts.Length; ++i)
        {
            string strPart = parts[i];
            if (Equals(strPart, ".."))
            {
                if (partsNormalized.Count == 0)
                {
                    if (!bIsAbsolute)
                        partsNormalized.Add("..");
                }
                else if (Equals(partsNormalized.Last(), ".."))
                {
                    partsNormalized.Add("..");
                }
                else
                {
                    partsNormalized.RemoveAt(partsNormalized.Count - 1);
                }
            }
            else if (strPart.Length > 0 && !Equals(strPart, "."))
            {
                partsNormalized.Add(strPart);
            }
        }

        var sbNormalized = new StringBuilder();

        //
        // 2. Add correct prefix to formatted path
        //

        if (bIsAbsolute)
        {
            sbNormalized.Append(parts[0]).Append('/');
        }
        else if (partsNormalized.Count == 0 || !Equals(partsNormalized[0], ".."))
        {
            sbNormalized.Append("./");
        }

        //
        // 3. Join path components to form the normalized path
        //

        for (int i = 0; i < partsNormalized.Count; ++i)
        {
            if (i > 0)
                sbNormalized.Append('/');
            sbNormalized.Append(partsNormalized[i]);
        }

        string strNormalized = sbNormalized.ToString();
        if (strNormalized.HasSuffix(".."))
            strNormalized += "/";
        return strNormalized;
    }

    private string GetConfigPath(string strFileName)
    {
        string strDirectory = FilePath;

        foreach (string strSuffix in s_initSuffixes)
        {
            if (strDirectory.HasSuffix(strSuffix))
            {
                strDirectory = strDirectory.RemoveSuffix(strSuffix);
                return strDirectory + "/" + strFileName;
            }
        }

        foreach (string strSuffix in s_suffixes)
        {
            if (strDirectory.HasSuffix(strSuffix))
            {
                strDirectory = strDirectory.RemoveSuffix(strSuffix);
                return strDirectory + "/" + strFileName;
            }
        }

        return strDirectory + "/" + strFileName;
    }

    private static string GetModulePath(string strFilePath)
    {
        strFilePath = strFilePath.Replace('\\', '/');

        if (LuauRequireByString.IsAbsolutePath(strFilePath))
        {
            int nPosFirstSlash = strFilePath.RequiredIndexOfFirstSlash();
            strFilePath = strFilePath.Remove(0, nPosFirstSlash);
        }

        foreach (string strSuffix in s_initSuffixes)
        {
            if (strFilePath.HasSuffix(strSuffix))
                return strFilePath.RemoveSuffix(strSuffix);
        }

        foreach (string strSuffix in s_suffixes)
        {
            if (strFilePath.HasSuffix(strSuffix))
                return strFilePath.RemoveSuffix(strSuffix);
        }

        return strFilePath;
    }

    private class ResolvedRealPath
    {
        public luarequire_NavigateResult Result { get; }
        public string Path { get; init; }

        private ResolvedRealPath(luarequire_NavigateResult eResult)
        {
            Result = eResult;
            Path = "";
        }

        public static ResolvedRealPath For(string strModulePath)
        {
            int nPosLastSlash = strModulePath.RequiredIndexOfLastSlash();
            string strLastPart = strModulePath.Substring(nPosLastSlash + 1);
            string? strSuffix = null;

            if (!Equals(strLastPart, "init"))
            {
                foreach (string strPotentialSuffix in s_suffixes)
                {
                    if (LuauRequireByString.FileExists(strModulePath + strPotentialSuffix))
                    {
                        if (strSuffix is not null)
                            return new ResolvedRealPath(luarequire_NavigateResult.NAVIGATE_AMBIGUOUS);

                        strSuffix = strPotentialSuffix;
                    }
                }
            }

            if (LuauRequireByString.DirectoryExists(strModulePath))
            {
                if (strSuffix is not null)
                    return new ResolvedRealPath(luarequire_NavigateResult.NAVIGATE_AMBIGUOUS);

                foreach (string strPotentialSuffix in s_initSuffixes)
                {
                    if (LuauRequireByString.FileExists(strModulePath + strPotentialSuffix))
                    {
                        if (strSuffix is not null)
                            return new ResolvedRealPath(luarequire_NavigateResult.NAVIGATE_AMBIGUOUS);

                        strSuffix = strPotentialSuffix;
                    }
                }

                strSuffix ??= ""; // if no suffix was found yet strModulePath (without suffix) is the real path
            }

            if (strSuffix is null)
                return new ResolvedRealPath(luarequire_NavigateResult.NAVIGATE_NOT_FOUND);

            return new ResolvedRealPath(luarequire_NavigateResult.NAVIGATE_SUCCESS)
            {
                Path = strModulePath + strSuffix,
            };
        }
    }
}
