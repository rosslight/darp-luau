using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Darp.Luau.Native;
using Darp.Luau.Utils;
using static Darp.Luau.Native.LuauNative;

[assembly: DisableRuntimeMarshalling]

namespace Darp.Luau.Internal.Require;

/// <summary>
/// Implements require-by-string
/// See https://github.com/luau-lang/luau
/// </summary>
internal static unsafe partial class LuauRequireByString
{
    /// <summary>Enables file-backed script modules for <see cref="LuauState"/>.</summary>
    /// <param name="state"></param>
    public static void EnableScriptModules(this LuauState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();

        LuauModuleContext context = state.GetOrCreateRequireContext();
        context.ScriptModulesEnabled = true;
    }

    internal static LuauModuleContext EnsureRequireInstalled(this LuauState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();

        return state.GetOrCreateRequireContext();
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

    internal static LuauModuleContext CreateAndInstallContext(LuauState state)
    {
        var context = new LuauModuleContext(state);
        try
        {
            InstallRequireFunction(state, context);
            return context;
        }
        catch
        {
            context.Dispose();
            throw;
        }
    }

    private static void InstallRequireFunction(LuauState state, LuauModuleContext context)
    {
#if DEBUG
        using var guard = new StackGuard(state.L, expectedDelta: 0);
#endif
        using LuauTable environment = state.CreateEnvironment();
        using LuauFunction resolveRequire = CreateContextFunction(state, context, &ResolveRequire);
        using LuauFunction findCurrentFile = CreateContextFunction(state, context, &FindCurrentFile);
        using LuauFunction proxyRequire = CreateProxyRequireFunction(state, context);

        environment.Set("resolve_require", resolveRequire);
        environment.Set("find_current_file", findCurrentFile);
        environment.Set("proxyrequire", proxyRequire);

        using LuauFunction require = state
            .Load(
                """
                local path = ...
                if type(path) ~= "string" then
                  error("bad argument #1 to 'require' (string expected, got " .. type(path) .. ")")
                end

                local kind, value = resolve_require(path)
                if kind == 1 then
                  return value
                end
                if kind == 2 then
                  return proxyrequire(path, find_current_file())
                end
                if kind == 3 then
                  error(value)
                end
                error("module '" .. path .. "' is not registered")
                """
            )
            .WithName("=darp_require")
            .WithEnvironment(environment)
            .ToFunction();

        state.Globals.Set("require", require);
    }

    private static LuauFunction CreateContextFunction(
        LuauState state,
        LuauModuleContext context,
        delegate* unmanaged[Cdecl]<lua_State*, int> callback
    )
    {
#if DEBUG
        using var guard = new StackGuard(state.L, expectedDelta: 0);
#endif
        lua_pushlightuserdata(state.L, context.ToVoidPtr());
        lua_pushcclosure(state.L, callback, null, 1);
        ulong reference = state.ReferenceTracker.TrackAndPopRef(state.L, -1);
        return new LuauFunction(state, reference);
    }

    private static LuauFunction CreateProxyRequireFunction(LuauState state, LuauModuleContext context)
    {
#if DEBUG
        using var guard = new StackGuard(state.L, expectedDelta: 0);
#endif
        _ = luarequire_pushproxyrequire(state.L, &InitRequireConfig, context.ToVoidPtr());
        ulong reference = state.ReferenceTracker.TrackAndPopRef(state.L, -1);
        return new LuauFunction(state, reference);
    }

    internal static bool IsScriptModulePath(string path) =>
        path.StartsWith("./", StringComparison.Ordinal)
        || path.StartsWith("../", StringComparison.Ordinal)
        || path.StartsWith('@');

    internal static bool IsInvalidScriptModulePath(string path) =>
        path.Contains('/', StringComparison.Ordinal) || path.Contains('\\', StringComparison.Ordinal);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int ResolveRequire(lua_State* L)
    {
        LuauModuleContext context = LuauModuleContext.FromUpvalue(L);
        if (!LuauStateMarshal.TryGetString(L, 1, out ReadOnlySpan<byte> utf8ModuleName))
        {
            lua_pushinteger(L, (int)LuauModuleContext.RequireResolutionKind.Error);
            LuauStateMarshal.PushString(L, "bad argument #1 to 'require' (string expected)");
            return 2;
        }

        string moduleName = Encoding.UTF8.GetString(utf8ModuleName);
        LuauModuleContext.RequireResolution resolution = context.ResolveRequire(moduleName);
        context.PushResolution(L, resolution);

        return
            resolution.Kind
                is LuauModuleContext.RequireResolutionKind.HostModule
                    or LuauModuleContext.RequireResolutionKind.Error
            ? 2
            : 1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int FindCurrentFile(lua_State* L)
    {
        var debug = new lua_Debug();
        for (int level = 2; ; level++)
        {
            int status;
            fixed (byte* pWhat = "s\0"u8)
            {
                status = lua_getinfo(L, level, pWhat, &debug);
            }

            if (status == 0)
            {
                LuauStateMarshal.PushString(L, "=stdin");
                return 1;
            }

            if (debug.what is null || !NullTerminatedEquals(debug.what, "C"u8))
            {
                lua_pushstring(L, debug.source);
                return 1;
            }
        }
    }

    private static bool NullTerminatedEquals(byte* value, ReadOnlySpan<byte> expected)
    {
        for (int i = 0; i < expected.Length; i++)
        {
            if (value[i] != expected[i])
                return false;
        }

        return value[expected.Length] == 0;
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
        var context = LuauModuleContext.FromVoidPtr(ctx);
        LuauModuleNavigator navigator = context.Navigators[L];

        string strChunkName = new((sbyte*)requirerChunkname);
        if (Equals(strChunkName, ChunkNameStdIn))
            return navigator.ResetToStdIn();

        if (strChunkName.StartsWith(ChunkNamePrefix))
            return navigator.ResetToPath(strChunkName[1..]);

        return luarequire_NavigateResult.NAVIGATE_NOT_FOUND;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_NavigateResult JumpToAlias(lua_State* L, void* ctx, byte* path)
    {
        var context = LuauModuleContext.FromVoidPtr(ctx);
        LuauModuleNavigator navigator = context.Navigators[L];

        string strPath = new((sbyte*)path);
        if (!IsAbsolutePath(strPath))
            return luarequire_NavigateResult.NAVIGATE_NOT_FOUND;

        return navigator.ResetToPath(strPath);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_NavigateResult ToParent(lua_State* L, void* ctx)
    {
        var context = LuauModuleContext.FromVoidPtr(ctx);
        LuauModuleNavigator navigator = context.Navigators[L];

        return navigator.ToParent();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_NavigateResult ToChild(lua_State* L, void* ctx, byte* name)
    {
        var context = LuauModuleContext.FromVoidPtr(ctx);
        LuauModuleNavigator navigator = context.Navigators[L];

        string strName = new((sbyte*)name);
        return navigator.ToChild(strName);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static bool IsModulePresent(lua_State* L, void* ctx)
    {
        var context = LuauModuleContext.FromVoidPtr(ctx);
        LuauModuleNavigator navigator = context.Navigators[L];

        return FileExists(navigator.FilePath);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_WriteResult GetChunkname(
        lua_State* L,
        void* ctx,
        byte* buffer,
        nuint bufferSize,
        nuint* sizeOut
    )
    {
        var context = LuauModuleContext.FromVoidPtr(ctx);
        LuauModuleNavigator navigator = context.Navigators[L];

        string strChunkName = ChunkNamePrefix + navigator.FilePath;
        return Write(strChunkName, buffer, bufferSize, sizeOut);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_WriteResult GetLoadname(
        lua_State* L,
        void* ctx,
        byte* buffer,
        nuint bufferSize,
        nuint* sizeOut
    )
    {
        var context = LuauModuleContext.FromVoidPtr(ctx);
        LuauModuleNavigator navigator = context.Navigators[L];

        string strLoadName = navigator.AbsoluteFilePath;
        return Write(strLoadName, buffer, bufferSize, sizeOut);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_WriteResult GetCacheKey(
        lua_State* L,
        void* ctx,
        byte* buffer,
        nuint bufferSize,
        nuint* sizeOut
    )
    {
        var context = LuauModuleContext.FromVoidPtr(ctx);
        LuauModuleNavigator navigator = context.Navigators[L];

        string strCacheKey = navigator.AbsoluteFilePath;
        return Write(strCacheKey, buffer, bufferSize, sizeOut);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_ConfigStatus GetConfigStatus(lua_State* L, void* ctx)
    {
        var context = LuauModuleContext.FromVoidPtr(ctx);
        LuauModuleNavigator navigator = context.Navigators[L];

        return navigator.GetConfigStatus();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_WriteResult GetConfig(
        lua_State* L,
        void* ctx,
        byte* buffer,
        nuint bufferSize,
        nuint* sizeOut
    )
    {
        var context = LuauModuleContext.FromVoidPtr(ctx);
        LuauModuleNavigator navigator = context.Navigators[L];

        string? strConfig = navigator.GetConfig();
        return Write(strConfig, buffer, bufferSize, sizeOut);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Load(lua_State* L, void* ctx, byte* path, byte* chunkname, byte* loadname)
    {
        string strPath = new((sbyte*)path);
        string strChunkName = new((sbyte*)chunkname);
        string strLoadName = new((sbyte*)loadname);

        var context = LuauModuleContext.FromVoidPtr(ctx);
        context.LoadError = null;

        int nResults = 1; // default number of results pushed onto stack
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: nResults);
#endif

        string? strContent = ReadFile(strLoadName);
        if (strContent is null)
        {
            nResults = ReportLoadError(L, context, $"could not read file '{strChunkName}'");
        }
        else
        {
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
                nuint nSizeByteCode = 0;
                byte* pByteCode = luau_compile(pSource, (nuint)spanSource.Length, null, &nSizeByteCode);
                try
                {
                    int nStatus = luau_load(ML, chunkname, pByteCode, nSizeByteCode, 0);
                    bOk = nStatus == 0;
                    if (!bOk)
                    {
                        if (lua_isstring(ML, -1) == 0)
                        {
                            nResults = ReportLoadError(ML, context, $"unknown error while loading module '{strPath}'");
                        }
                        else
                        {
                            string strMsg = new((sbyte*)lua_tostring(ML, -1));
                            nResults = ReportLoadError(
                                ML,
                                context,
                                $"error while loading module '{strPath}': {strMsg}"
                            );
                        }
                    }
                }
                finally
                {
                    luau_free(pByteCode);
                }
            }

            if (bOk)
            {
                int nStatus = lua_resume(ML, L, 0);
                if (nStatus == (int)lua_Status.LUA_OK)
                {
                    if (lua_gettop(ML) != 1)
                    {
                        // empty stack
                        while (lua_gettop(ML) > 0)
                            lua_pop(ML, 1);

                        nResults = ReportLoadError(ML, context, $"module '{strPath}' must return a single value");
                    }
                }
                else if (nStatus == (int)lua_Status.LUA_YIELD)
                {
                    nResults = ReportLoadError(ML, context, $"module '{strPath}' can not yield");
                }
                else if (lua_isstring(ML, -1) == 0)
                {
                    nResults = ReportLoadError(ML, context, $"unknown error while running module '{strPath}'");
                }
                else
                {
                    string strMsg = new((sbyte*)lua_tostring(ML, -1));
                    nResults = ReportLoadError(ML, context, $"error while running module '{strPath}': {strMsg}");
                }
            }

            // add ML results (success or reported error) to L stack
            lua_xmove(ML, L, nResults);

            // remove ML thread from L stack
            lua_remove(L, -(nResults + 1));
        }

#if DEBUG
        guard.OverwriteExpectedDelta(nResults);
#endif
        return nResults;
    }

    private static int ReportLoadError(lua_State* L, LuauModuleContext context, string strMsg)
    {
        context.LoadError = strMsg;

        // push TWO objects onto stack to indicate an error.
        // the caller of function Load reports this as error "module must return a single value".
        lua_pushnil(L);
        lua_pushnil(L);
        return 2; // number of objects pushed onto stack
    }

    private static luarequire_WriteResult Write(
        string? strSrc,
        byte* bufDest,
        nuint nSizeBufDest,
        nuint* nSizeBufDestOut
    )
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

    internal static bool IsAbsolutePath(string strPath)
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

    internal static bool FileExists(string? strPath)
    {
        if (OperatingSystem.IsWindows() && strPath is not null)
            strPath = strPath.Replace('/', '\\');

        return File.Exists(strPath);
    }

    internal static bool DirectoryExists(string? strPath)
    {
        if (OperatingSystem.IsWindows() && strPath is not null)
            strPath = strPath.Replace('/', '\\');

        return Directory.Exists(strPath);
    }

    internal static int RequiredIndexOfFirstSlash(this string str)
    {
        int nPos = str.IndexOf('/', StringComparison.InvariantCulture);
        if (nPos < 0)
            throw new LuaException("No first slash found");
        return nPos;
    }

    internal static int RequiredIndexOfLastSlash(this string str)
    {
        int nPos = str.LastIndexOf('/');
        if (nPos < 0)
            throw new LuaException("No last slash found");
        return nPos;
    }

    internal static bool HasSuffix(this string str, string strSuffix)
    {
        return str.EndsWith(strSuffix, StringComparison.InvariantCulture);
    }

    internal static string RemoveSuffix(this string str, string strSuffix)
    {
        return str.Remove(str.Length - strSuffix.Length);
    }

    internal static string? ReadFile(string strFileName)
    {
        try
        {
            if (FileExists(strFileName))
                return File.ReadAllText(strFileName);
        }
        catch { }

        return null;
    }
}
