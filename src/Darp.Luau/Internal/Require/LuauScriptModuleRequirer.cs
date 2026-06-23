using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Darp.Luau.Native;
using Darp.Luau.Utils;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau.Internal.Require;

/// <summary> A require-by-string requirer that uses a virtual file system to resolve module paths. </summary>
/// <seealso href="https://github.com/luau-lang/luau/blob/master/CLI/src/ReplRequirer.cpp"/>
internal sealed unsafe class LuauScriptModuleRequirer : IDisposable
{
    private const byte ChunkNamePrefix = (byte)'@';
    private readonly ILuauFileSystem _virtualFileSystem;
    private readonly Lock _navigatorLock = new();
    private readonly Dictionary<nint, LuauModuleNavigator> _navigators = [];
    private GCHandle _handle;
    private string? _pendingLoadError;

    /// <summary> A require-by-string requirer that uses a virtual file system to resolve module paths. </summary>
    /// <param name="virtualFileSystem">A virtual file system for abstract file operations</param>
    /// <seealso href="https://github.com/luau-lang/luau/blob/master/CLI/src/ReplRequirer.cpp"/>
    public LuauScriptModuleRequirer(ILuauFileSystem virtualFileSystem)
    {
        _virtualFileSystem = virtualFileSystem;
        _handle = GCHandle.Alloc(this);
    }

    public bool Enabled { get; set; }

    public RequireResolution Resolve(string path)
    {
        if (IsScriptModulePath(path))
        {
            _pendingLoadError = null;
            return Enabled
                ? RequireResolution.ScriptModule
                : RequireResolution.LoadError("script module require is not enabled");
        }

        return IsInvalidScriptModulePath(path)
            ? RequireResolution.LoadError("require path must start with a valid prefix: ./, ../, or @")
            : RequireResolution.NotFound;
    }

    internal string? TakePendingLoadError()
    {
        string? error = _pendingLoadError;
        _pendingLoadError = null;
        return error;
    }

    private static bool IsScriptModulePath(string path) =>
        path.StartsWith("./", StringComparison.Ordinal)
        || path.StartsWith("../", StringComparison.Ordinal)
        || path.StartsWith('@');

    private static bool IsInvalidScriptModulePath(string path) =>
        path.Contains('/', StringComparison.Ordinal) || path.Contains('\\', StringComparison.Ordinal);

    internal LuauFunction CreateProxyRequireFunction(LuauState state)
    {
#if DEBUG
        using var guard = new StackGuard(state.L, expectedDelta: 0);
#endif
        _ = luarequire_pushproxyrequire(state.L, &InitRequireConfig, ToVoidPtr());
        ulong reference = state.ReferenceTracker.TrackAndPopRef(state.L, -1);
        return new LuauFunction(state, reference);
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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static bool IsRequireAllowed(lua_State* L, void* ctx, byte* requirerChunkname)
    {
        // Require should be allowed only for
        // - Chunks with relative (preferred), or absolute paths -> have to start with an '@'
        // - Chunks that are named '=stdin'
        ReadOnlySpan<byte> chunkName = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(requirerChunkname);
        return chunkName.SequenceEqual("=stdin"u8) || chunkName.StartsWith(ChunkNamePrefix);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_NavigateResult Reset(lua_State* L, void* ctx, byte* requirerChunkname)
    {
        LuauScriptModuleRequirer req = FromVoidPtr(ctx);

        ReadOnlySpan<byte> chunkName = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(requirerChunkname);
        if (chunkName.SequenceEqual("=stdin"u8))
            return req.NavigatorFor(L).ResetToStdIn();
        if (chunkName.StartsWith(ChunkNamePrefix))
            return req.NavigatorFor(L).ResetToPath(Encoding.UTF8.GetString(chunkName[1..]));

        return luarequire_NavigateResult.NAVIGATE_NOT_FOUND;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_NavigateResult JumpToAlias(lua_State* L, void* ctx, byte* path)
    {
        LuauScriptModuleRequirer req = FromVoidPtr(ctx);

        string strPath = new((sbyte*)path);
        if (!FileUtils.IsAbsolutePath(strPath))
            return luarequire_NavigateResult.NAVIGATE_NOT_FOUND;

        return req.NavigatorFor(L).ResetToPath(strPath);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_NavigateResult ToParent(lua_State* L, void* ctx)
    {
        LuauScriptModuleRequirer req = FromVoidPtr(ctx);
        return req.NavigatorFor(L).ToParent();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_NavigateResult ToChild(lua_State* L, void* ctx, byte* name)
    {
        LuauScriptModuleRequirer req = FromVoidPtr(ctx);

        string strName = new((sbyte*)name);
        return req.NavigatorFor(L).ToChild(strName);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static bool IsModulePresent(lua_State* L, void* ctx)
    {
        LuauScriptModuleRequirer req = FromVoidPtr(ctx);
        LuauModuleNavigator navigator = req.NavigatorFor(L);

        return req._virtualFileSystem.FileExists(navigator.RealPath);
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
        LuauScriptModuleRequirer req = FromVoidPtr(ctx);
        LuauModuleNavigator navigator = req.NavigatorFor(L);

        return Write($"@{navigator.RealPath}", buffer, bufferSize, sizeOut);
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
        LuauScriptModuleRequirer req = FromVoidPtr(ctx);
        LuauModuleNavigator navigator = req.NavigatorFor(L);

        return Write(navigator.AbsoluteRealPath, buffer, bufferSize, sizeOut);
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
        LuauScriptModuleRequirer req = FromVoidPtr(ctx);
        LuauModuleNavigator navigator = req.NavigatorFor(L);

        return Write(navigator.AbsoluteRealPath, buffer, bufferSize, sizeOut);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_ConfigStatus GetConfigStatus(lua_State* L, void* ctx)
    {
        LuauScriptModuleRequirer req = FromVoidPtr(ctx);
        LuauModuleNavigator navigator = req.NavigatorFor(L);

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
        LuauScriptModuleRequirer req = FromVoidPtr(ctx);
        LuauModuleNavigator navigator = req.NavigatorFor(L);

        return Write(navigator.GetConfig(), buffer, bufferSize, sizeOut);
    }

    /// <summary>
    /// Report a load error to the native proxy.
    /// This function stores the error message and returns to many values which the luau require library interprets as a failed load.
    ///
    /// This is a workaround because we cannot raise a lua_error in c# because we cannot longjmp across C# boundary.
    /// Instead, we will check <seealso cref="TakePendingLoadError"/> in the native proxy and raise a lua_error if it is not null.
    /// </summary>
    private int ReportLoadError(lua_State* L, string message)
    {
        _pendingLoadError = message;

        // Push two values so the native proxy reports this as a failed require, not a cached module value.
        lua_pushnil(L);
        lua_pushnil(L);
        return 2;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Load(lua_State* L, void* ctx, byte* path, byte* chunkname, byte* loadname)
    {
        string strPath = new((sbyte*)path);
        string strChunkName = new((sbyte*)chunkname);
        string strLoadName = new((sbyte*)loadname);

        LuauScriptModuleRequirer req = FromVoidPtr(ctx);
        req._pendingLoadError = null;

        int nResults = 1; // default number of results pushed onto stack
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: nResults);
#endif

        string? strContent = req._virtualFileSystem.ReadFile(strLoadName);
        if (strContent is null)
        {
            nResults = req.ReportLoadError(L, $"could not read file '{strChunkName}'");
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
                            nResults = req.ReportLoadError(ML, $"unknown error while loading module '{strPath}'");
                        }
                        else
                        {
                            string strMsg = new((sbyte*)lua_tostring(ML, -1));
                            nResults = req.ReportLoadError(ML, $"error while loading module '{strPath}': {strMsg}");
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

                        nResults = req.ReportLoadError(ML, $"module '{strPath}' must return a single value");
                    }
                }
                else if (nStatus == (int)lua_Status.LUA_YIELD)
                {
                    nResults = req.ReportLoadError(ML, $"module '{strPath}' can not yield");
                }
                else if (lua_isstring(ML, -1) == 0)
                {
                    nResults = req.ReportLoadError(ML, $"unknown error while running module '{strPath}'");
                }
                else
                {
                    string strMsg = new((sbyte*)lua_tostring(ML, -1));
                    nResults = req.ReportLoadError(ML, $"error while running module '{strPath}': {strMsg}");
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

    private static luarequire_WriteResult Write(
        string? strSrc,
        byte* bufDest,
        nuint nSizeBufDest,
        nuint* nSizeBufDestOut
    )
    {
        if (strSrc is null)
            return luarequire_WriteResult.WRITE_FAILURE;

        int byteCount = Encoding.UTF8.GetByteCount(strSrc);
        *nSizeBufDestOut = (nuint)byteCount;

        if (bufDest is null || nSizeBufDest < (nuint)byteCount)
            return luarequire_WriteResult.WRITE_BUFFER_TOO_SMALL;

        var buffer = new Span<byte>(bufDest, (int)nSizeBufDest);
        *nSizeBufDestOut = (nuint)Encoding.UTF8.GetBytes(strSrc, buffer);
        return luarequire_WriteResult.WRITE_SUCCESS;
    }

    private LuauModuleNavigator NavigatorFor(lua_State* L)
    {
        lock (_navigatorLock)
        {
            nint key = (nint)L;
            if (!_navigators.TryGetValue(key, out LuauModuleNavigator? navigator))
            {
                navigator = new LuauModuleNavigator(_virtualFileSystem);
                _navigators.Add(key, navigator);
            }

            return navigator;
        }
    }

    private void* ToVoidPtr()
    {
        return (void*)GCHandle.ToIntPtr(_handle);
    }

    private static LuauScriptModuleRequirer FromVoidPtr(void* pCtx)
    {
        var gchCtx = GCHandle.FromIntPtr((IntPtr)pCtx);
        return gchCtx.Target as LuauScriptModuleRequirer ?? throw new ArgumentException("invalid context pointer");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_handle.IsAllocated)
            _handle.Free();
    }
}
