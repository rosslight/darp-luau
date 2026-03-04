using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Darp.Luau.Native;
using static Darp.Luau.Native.LuauNative;

//TODO Find a more central location within project!?
[assembly: DisableRuntimeMarshalling]

namespace Darp.Luau;

public static unsafe class LuauRequireByString
{
    private static readonly Navigator s_navigator = new();

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
        return Equals(strChunkName, ChunkNameStdIn) || (strChunkName.Length > 0 && strChunkName[0] == ChunkNamePrefix);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_NavigateResult Reset(lua_State* L, void* ctx, byte* requirerChunkname)
    {
        string strChunkName = new((sbyte*)requirerChunkname);
        if (Equals(strChunkName, ChunkNameStdIn))
            return s_navigator.ResetToStdIn();

        if (strChunkName.Length > 0 && strChunkName[0] == ChunkNamePrefix)
            return s_navigator.ResetToPath(strChunkName[1..]);

        return luarequire_NavigateResult.NAVIGATE_NOT_FOUND;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_NavigateResult JumpToAlias(lua_State* L, void* ctx, byte* path)
    {
        string strPath = new((sbyte*)path);
        if (!Path.IsPathRooted(strPath))
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
        return File.Exists(s_navigator.FilePath);
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
        //TODO
        string strPath = new((sbyte*)path);
        string strChunkName = new((sbyte*)chunkname);
        string strLoadName = new((sbyte*)loadname);
        return 0;
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

    private static bool IsAbsolutePath([NotNull] string strPath)
    {
        if (OperatingSystem.IsWindows())
        {
            // Must either begin with "X:/", "X:\", "/", or "\", where X is a drive letter
            return (strPath.Length > 2
                        && char.IsLetter(strPath[0])
                        && strPath[1] == ':'
                        && (strPath[2] == '/' || strPath[2] == '\\'))
                    || (strPath.Length > 0
                        && (strPath[0] == '/' || strPath[0] == '\\'));
        }
        else
        {
            // Must begin with '/'
            return strPath.Length > 0 && strPath[0] == '/';
        }
    }

    public static string NormalizePath([NotNull] string strPath)
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
        if (strNormalized.EndsWith("..", StringComparison.InvariantCultureIgnoreCase))
            strNormalized += "/";

        return strNormalized;
    }

    private class Navigator
    {
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

            int nPosFirstSlash = RequiredIndexOfFirstSlash(AbsoluteFilePath);
            _strAbsolutePathPrefix = AbsoluteFilePath.Substring(0, nPosFirstSlash);

            return luarequire_NavigateResult.NAVIGATE_SUCCESS;
        }

        public luarequire_NavigateResult ResetToPath(string strPath)
        {
            strPath = NormalizePath(strPath);

            if (IsAbsolutePath(strPath))
            {
                _strModulePath = GetModulePath(strPath);
                _strAbsoluteModulePath = _strModulePath;

                int nPosFirstSlash = RequiredIndexOfFirstSlash(strPath);
                _strAbsolutePathPrefix = strPath.Substring(0, nPosFirstSlash);
            }
            else
            {
                _strModulePath = GetModulePath(strPath);
                string strJoinedPath = NormalizePath(Directory.GetCurrentDirectory() + "/" + strPath);
                _strAbsoluteModulePath = GetModulePath(strJoinedPath);

                int nPosFirstSlash = RequiredIndexOfFirstSlash(strJoinedPath);
                _strAbsolutePathPrefix = strJoinedPath.Substring(0, nPosFirstSlash);
            }

            return UpdateRealPaths();
        }

        public luarequire_NavigateResult ToParent()
        {
            //TODO
            return luarequire_NavigateResult.NAVIGATE_NOT_FOUND;
        }

        public luarequire_NavigateResult ToChild(string strName)
        {
            //TODO
            return luarequire_NavigateResult.NAVIGATE_NOT_FOUND;
        }

        public luarequire_ConfigStatus GetConfigStatus()
        {
            //TODO
            return luarequire_ConfigStatus.CONFIG_ABSENT;
        }

        public string? GetConfig()
        {
            //TODO
            return null;
        }

        private luarequire_NavigateResult UpdateRealPaths()
        {
            //TODO
            return luarequire_NavigateResult.NAVIGATE_NOT_FOUND;
        }

        private static readonly string[] s_suffixes = [".luau", ".lua"];
        private static readonly string[] s_initSuffixes = ["/init.luau", "/init.lua"];

        private static string GetModulePath(string strFilePath)
        {
            strFilePath = strFilePath.Replace('\\', '/');

            if (IsAbsolutePath(strFilePath))
            {
                int nPosFirstSlash = RequiredIndexOfFirstSlash(strFilePath);
                strFilePath = strFilePath.Remove(0, nPosFirstSlash);
            }

            foreach (string strSuffix in s_initSuffixes)
            {
                if (strFilePath.EndsWith(strSuffix, StringComparison.InvariantCulture))
                    return strFilePath.Remove(strFilePath.Length - strSuffix.Length);
            }

            foreach (string strSuffix in s_suffixes)
            {
                if (strFilePath.EndsWith(strSuffix, StringComparison.InvariantCulture))
                    return strFilePath.Remove(strFilePath.Length - strSuffix.Length);
            }

            return strFilePath;
        }

        private static int RequiredIndexOfFirstSlash(string strPath)
        {
            int nPosFirstSlash = strPath.IndexOf('/', StringComparison.InvariantCultureIgnoreCase);
            if (nPosFirstSlash < 0)
                throw new LuaException("No slash found");
            return nPosFirstSlash;
        }
    }
}

