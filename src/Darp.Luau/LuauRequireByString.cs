using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Darp.Luau.Native;
using static Darp.Luau.Native.LuauNative;

[assembly: DisableRuntimeMarshalling]

namespace Darp.Luau;

/// <summary>
/// Implements require-by-string
/// See https://github.com/luau-lang/luau
/// </summary>
public static unsafe partial class LuauRequireByString
{
    private static readonly Navigator s_navigator = new();

    /// <summary>Enables require-by-string for LuauState</summary>
    /// <param name="state"></param>
    public static void EnableRequireByString(this LuauState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        luaopen_require(state.L, &InitRequireConfig, null);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void InitRequireConfig(luarequire_Configuration* config)
    {
        config->is_require_allowed = &IsRequireAllowed;
        config->reset = &Reset;
        config->jump_to_alias = &JumpToAlias;
        config->to_parent = &ToParent;
        config->to_child = &ToChild;
        config->is_module_present = &IsModulePresent;
        config->get_chunkname = &GetChunkname;
        config->get_loadname = &GetLoadname;
        config->get_cache_key = &GetCacheKey;
        config->get_config_status = &GetConfigStatus;
        config->get_config = &GetConfig;
        config->load = &Load;
    }

    private const char ChunkNamePrefix = '@';
    private const string ChunkNameStdIn = "=stdin";

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static bool IsRequireAllowed(lua_State* L, void* ctx, byte* requirerChunkname)
    {
        string strChunkName = new((sbyte*)requirerChunkname);
        return Equals(strChunkName, ChunkNameStdIn) || strChunkName.StartsWith(ChunkNamePrefix);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_NavigateResult Reset(lua_State* L, void* ctx, byte* requirerChunkname)
    {
        string strChunkName = new((sbyte*)requirerChunkname);
        if (Equals(strChunkName, ChunkNameStdIn))
            return s_navigator.ResetToStdIn();

        if (strChunkName.StartsWith(ChunkNamePrefix))
            return s_navigator.ResetToPath(strChunkName[1..]);

        return luarequire_NavigateResult.NAVIGATE_NOT_FOUND;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_NavigateResult JumpToAlias(lua_State* L, void* ctx, byte* path)
    {
        string strPath = new((sbyte*)path);
        if (!IsAbsolutePath(strPath))
            return luarequire_NavigateResult.NAVIGATE_NOT_FOUND;

        return s_navigator.ResetToPath(strPath);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_NavigateResult ToParent(lua_State* L, void* ctx)
    {
        return s_navigator.ToParent();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_NavigateResult ToChild(lua_State* L, void* ctx, byte* name)
    {
        string strName = new((sbyte*)name);
        return s_navigator.ToChild(strName);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static bool IsModulePresent(lua_State* L, void* ctx)
    {
        return FileExists(s_navigator.FilePath);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_WriteResult GetChunkname(lua_State* L, void* ctx, byte* buffer, nuint bufferSize, nuint* sizeOut)
    {
        string strChunkName = ChunkNamePrefix + s_navigator.FilePath;
        return Write(strChunkName, buffer, bufferSize, sizeOut);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_WriteResult GetLoadname(lua_State* L, void* ctx, byte* buffer, nuint bufferSize, nuint* sizeOut)
    {
        string strLoadName = s_navigator.AbsoluteFilePath;
        return Write(strLoadName, buffer, bufferSize, sizeOut);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_WriteResult GetCacheKey(lua_State* L, void* ctx, byte* buffer, nuint bufferSize, nuint* sizeOut)
    {
        string strCacheKey = s_navigator.AbsoluteFilePath;
        return Write(strCacheKey, buffer, bufferSize, sizeOut);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_ConfigStatus GetConfigStatus(lua_State* L, void* ctx)
    {
        return s_navigator.GetConfigStatus();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_WriteResult GetConfig(lua_State* L, void* ctx, byte* buffer, nuint bufferSize, nuint* sizeOut)
    {
        string? strConfig = s_navigator.GetConfig();
        return Write(strConfig, buffer, bufferSize, sizeOut);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Load(lua_State* L, void* ctx, byte* path, byte* chunkname, byte* loadname)
    {
        string strPath = new((sbyte*)path);
        string strChunkName = new((sbyte*)chunkname);
        string strLoadName = new((sbyte*)loadname);

        string? strContent = ReadFile(strLoadName);
        if (strContent is null)
        {
            LuauStateMarshal.PushString(L, $"could not read file '{strChunkName}'");
            return 1;
        }

        // module needs to run in a new thread, isolated from the rest
        // note: we create ML on main thread so that it doesn't inherit environment of L
        lua_State* GL = lua_mainthread(L);
        lua_State* ML = lua_newthread(GL);
        lua_xmove(GL, L, 1);

        // new thread needs to have the globals sandboxed
        luaL_sandboxthread(ML);

        bool bOk = false;
        ReadOnlySpan<byte> spanSource = Encoding.UTF8.GetBytes(strContent);
        fixed (byte* pSource = spanSource)
        {
            nuint nByteCodeSize = 0;
            byte* pByteCode = luau_compile(pSource, (nuint)spanSource.Length, null, &nByteCodeSize);
            int nStatus = luau_load(ML, chunkname, pByteCode, nByteCodeSize, 0);
            bOk = nStatus == 0;
        }

        if (bOk)
        {
            int nStatus = lua_resume(ML, L, 0);
            if (nStatus == (int)lua_Status.LUA_OK)
            {
                if (lua_gettop(ML) != 1)
                    LuauStateMarshal.PushString(ML, $"module '{strPath}' must return a single value");
            }
            else if (nStatus == (int)lua_Status.LUA_YIELD)
            {
                LuauStateMarshal.PushString(ML, $"module '{strPath}' can not yield");
            }
            else if (lua_isstring(ML, -1) == 0)
            {
                LuauStateMarshal.PushString(ML, $"unknown error while running module '{strPath}'");
            }
            else
            {
                string strMsg = new((sbyte*)lua_tostring(ML, -1));
                LuauStateMarshal.PushString(ML, $"error while running module '{strPath}': {strMsg}");
            }

            // add ML result to L stack
            lua_xmove(ML, L, 1);
        }

        // remove ML thread from L stack
        lua_remove(L, -2);

        // added one value to L stack: module result
        return 1;
    }

    private static luarequire_WriteResult Write(string? strSrc, byte* bufDest, nuint nSizeBufDest, nuint* nSizeBufDestOut)
    {
        if (strSrc is null)
            return luarequire_WriteResult.WRITE_FAILURE;

        byte[] bufSrc = Encoding.UTF8.GetBytes(strSrc);
        int nSizeBufSrc = bufSrc.Length;

        *nSizeBufDestOut = (nuint)(nSizeBufSrc + 1);
        if (*nSizeBufDestOut > nSizeBufDest)
            return luarequire_WriteResult.WRITE_BUFFER_TOO_SMALL;

        Marshal.Copy(bufSrc, 0, (IntPtr)bufDest, nSizeBufSrc);
        *(bufDest + nSizeBufSrc) = 0;
        return luarequire_WriteResult.WRITE_SUCCESS;
    }

    [GeneratedRegex(@"^([a-zA-Z]:)?[\\/]")]
    private static partial Regex RegexAbsoluteWinPath();

    private static bool IsAbsolutePath(string strPath)
    {
        if (OperatingSystem.IsWindows())
        {
            // Must either begin with "X:/", "X:\", "/", or "\", where X is a drive letter
            return RegexAbsoluteWinPath().IsMatch(strPath);
        }
        else
        {
            // Must begin with '/'
            return strPath.StartsWith('/');
        }
    }

    private static bool FileExists(string? strPath)
    {
        if (OperatingSystem.IsWindows() && strPath is not null)
            strPath = strPath.Replace('/', '\\');

        return File.Exists(strPath);
    }

    private static bool DirectoryExists(string? strPath)
    {
        if (OperatingSystem.IsWindows() && strPath is not null)
            strPath = strPath.Replace('/', '\\');

        return Directory.Exists(strPath);
    }

    private static int RequiredIndexOfFirstSlash(this string str)
    {
        int nPos = str.IndexOf('/', StringComparison.InvariantCulture);
        if (nPos < 0)
            throw new LuaException("No first slash found");
        return nPos;
    }

    private static int RequiredIndexOfLastSlash(this string str)
    {
        int nPos = str.LastIndexOf('/');
        if (nPos < 0)
            throw new LuaException("No last slash found");
        return nPos;
    }

    private static bool HasSuffix(this string str, string strSuffix)
    {
        return str.EndsWith(strSuffix, StringComparison.InvariantCulture);
    }

    private static string RemoveSuffix(this string str, string strSuffix)
    {
        return str.Remove(str.Length - strSuffix.Length);
    }

    private static string? ReadFile(string strFileName)
    {
        try
        {
            if (FileExists(strFileName))
                return File.ReadAllText(strFileName);
        }
        catch
        {
        }

        return null;
    }

    internal class Navigator()
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

            if (IsAbsolutePath(strPath))
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
            bool bConfig = FileExists(GetConfigPath(ConfigName));
            bool bLuauConfig = FileExists(GetConfigPath(LuauConfigName));

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
                luarequire_ConfigStatus.CONFIG_PRESENT_JSON => ReadFile(GetConfigPath(ConfigName)),
                luarequire_ConfigStatus.CONFIG_PRESENT_LUAU => ReadFile(GetConfigPath(LuauConfigName)),
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

            FilePath = IsAbsolutePath(resolved.Path) ? _strAbsolutePathPrefix + resolved.Path : resolved.Path;
            AbsoluteFilePath = _strAbsolutePathPrefix + absoluteResolved.Path;
            return luarequire_NavigateResult.NAVIGATE_SUCCESS;
        }

        internal static string NormalizePath(string strPath)
        {
            string[] parts = strPath.Split('/', '\\');
            bool bIsAbsolute = IsAbsolutePath(strPath);

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

            if (IsAbsolutePath(strFilePath))
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
            public string Path { get; }

            private ResolvedRealPath(luarequire_NavigateResult eResult, string strPath = "")
            {
                Result = eResult;
                Path = strPath;
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
                        if (FileExists(strModulePath + strPotentialSuffix))
                        {
                            if (strSuffix is not null)
                                return new ResolvedRealPath(luarequire_NavigateResult.NAVIGATE_AMBIGUOUS);

                            strSuffix = strPotentialSuffix;
                        }
                    }
                }

                if (DirectoryExists(strModulePath))
                {
                    if (strSuffix is not null)
                        return new ResolvedRealPath(luarequire_NavigateResult.NAVIGATE_AMBIGUOUS);

                    foreach (string strPotentialSuffix in s_initSuffixes)
                    {
                        if (FileExists(strModulePath + strPotentialSuffix))
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

                return new ResolvedRealPath(luarequire_NavigateResult.NAVIGATE_SUCCESS, strModulePath + strSuffix);
            }
        }
    }
}
