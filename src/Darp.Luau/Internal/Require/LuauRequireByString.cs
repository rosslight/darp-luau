using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Darp.Luau.Native;
using Darp.Luau.Utils;
using static Darp.Luau.Native.LuauNative;

[assembly: DisableRuntimeMarshalling]

namespace Darp.Luau;

/// <summary>
/// Implements require-by-string
/// See https://github.com/luau-lang/luau
/// </summary>
internal static unsafe partial class LuauRequireByString
{
    /// <summary>internal require context</summary>
    internal sealed class Context : IDisposable, IRequireContext
    {
        internal enum RequireResolutionKind
        {
            NotFound = 0,
            HostModule = 1,
            ScriptModule = 2,
            Error = 3,
        }

        internal readonly record struct RequireResolution(RequireResolutionKind Kind, LuauTable Module, string? Error)
        {
            public static RequireResolution NotFound { get; } = new(RequireResolutionKind.NotFound, default, null);

            public static RequireResolution ScriptModule { get; } =
                new(RequireResolutionKind.ScriptModule, default, null);

            public static RequireResolution LoadedHostModule(LuauTable module) =>
                new(RequireResolutionKind.HostModule, module, null);

            public static RequireResolution LoadError(string error) => new(RequireResolutionKind.Error, default, error);
        }

        private delegate RequireResolution RequireResolver(Context context, string moduleName);

        private sealed class HostModule(string name, LuauState.OnModuleLoad onLoad)
        {
            public string Name { get; } = name;
            public LuauState.OnModuleLoad OnLoad { get; } = onLoad;
            public LuauTable? CachedValue { get; set; }
            public bool IsLoading { get; set; }
        }

        private readonly LuauState _state;
        private GCHandle _handle;
        private readonly Dictionary<string, HostModule> _hostModules = new(StringComparer.Ordinal);
        private readonly List<RequireResolver> _resolvers;

        internal Context(LuauState state)
        {
            _state = state;
            _handle = GCHandle.Alloc(this);
            _resolvers = [ResolveHostModule, ResolveScriptModule];
        }

        internal Navigators Navigators { get; } = new();

        internal bool ScriptModulesEnabled { get; set; }

        public string? LoadError { get; internal set; }

        internal LuauState State => _state;

        internal void RegisterHostModule(string name, LuauState.OnModuleLoad onLoad)
        {
            if (!_hostModules.TryAdd(name, new HostModule(name, onLoad)))
                throw new InvalidOperationException($"Module '{name}' is already registered.");
        }

        internal unsafe void PushModule(lua_State* L, LuauTable module)
        {
            ulong handle = module.GetHandleOrThrow();
            RegistryReferenceTracker.TrackedReference trackedReference = _state.GetTrackedReferenceOrThrow(handle);
#if DEBUG
            using var guard = new StackGuard(_state.L, expectedDelta: (nint)L == (nint)_state.L ? 1 : 0);
#endif
#pragma warning disable CA2000 // The pushed value is returned to the caller stack.
            _ = trackedReference.PushToTop();
#pragma warning restore CA2000
            if ((nint)L != (nint)_state.L)
                lua_xmove(_state.L, L, 1);
        }

        internal RequireResolution ResolveRequire(string moduleName)
        {
            foreach (RequireResolver resolver in _resolvers)
            {
                RequireResolution resolution = resolver(this, moduleName);
                if (resolution.Kind != RequireResolutionKind.NotFound)
                    return resolution;
            }

            return RequireResolution.NotFound;
        }

        internal void PushResolution(lua_State* L, RequireResolution resolution)
        {
            lua_pushinteger(L, (int)resolution.Kind);
            switch (resolution.Kind)
            {
                case RequireResolutionKind.HostModule:
                    PushModule(L, resolution.Module);
                    break;
                case RequireResolutionKind.Error:
                    LuauStateMarshal.PushString(L, resolution.Error ?? "require failed");
                    break;
            }
        }

        private static RequireResolution ResolveHostModule(Context context, string name)
        {
            if (!context._hostModules.TryGetValue(name, out HostModule? registration))
                return RequireResolution.NotFound;

            if (registration.CachedValue is { } cached)
                return RequireResolution.LoadedHostModule(cached);

            if (registration.IsLoading)
                return RequireResolution.LoadError($"module '{name}' is already loading");

            registration.IsLoading = true;
            try
            {
                LuauTable loaded = context._state.CreateTable();
                try
                {
                    registration.OnLoad(context._state, loaded);
                }
                catch
                {
                    loaded.Dispose();
                    throw;
                }

                registration.CachedValue = loaded;
                return RequireResolution.LoadedHostModule(loaded);
            }
            catch (Exception exception)
            {
                return RequireResolution.LoadError(
                    $"failed to load module '{name}': {exception.GetType().Name}: {exception.Message}"
                );
            }
            finally
            {
                registration.IsLoading = false;
            }
        }

        private static RequireResolution ResolveScriptModule(Context context, string path)
        {
            if (IsScriptModulePath(path))
            {
                return context.ScriptModulesEnabled
                    ? RequireResolution.ScriptModule
                    : RequireResolution.LoadError("script module require is not enabled");
            }

            return IsInvalidScriptModulePath(path)
                ? RequireResolution.LoadError("require path must start with a valid prefix: ./, ../, or @")
                : RequireResolution.NotFound;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            foreach (HostModule module in _hostModules.Values)
            {
                module.CachedValue?.Dispose();
                module.CachedValue = null;
            }
            _hostModules.Clear();

            if (_handle.IsAllocated)
                _handle.Free();
        }

        internal void* ToVoidPtr()
        {
            return (void*)GCHandle.ToIntPtr(_handle);
        }

        internal static Context FromVoidPtr(void* pCtx)
        {
            var gchCtx = GCHandle.FromIntPtr((IntPtr)pCtx);
            return gchCtx.Target as Context ?? throw new ArgumentException("invalid context pointer");
        }

        internal static Context FromUpvalue(lua_State* L)
        {
            void* pCtx = lua_tolightuserdata(L, unchecked((int)lua_upvalueindex(1)));
            return FromVoidPtr(pCtx);
        }
    }

    /// <summary>Enables file-backed script modules for <see cref="LuauState"/>.</summary>
    /// <param name="state"></param>
    public static void EnableScriptModules(this LuauState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();

        Context context = state.GetOrCreateRequireContext();
        context.ScriptModulesEnabled = true;
    }

    internal static Context EnsureRequireInstalled(this LuauState state)
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

    internal static Context CreateAndInstallContext(LuauState state)
    {
        var context = new Context(state);
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

    private static void InstallRequireFunction(LuauState state, Context context)
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
        Context context,
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

    private static LuauFunction CreateProxyRequireFunction(LuauState state, Context context)
    {
#if DEBUG
        using var guard = new StackGuard(state.L, expectedDelta: 0);
#endif
        _ = luarequire_pushproxyrequire(state.L, &InitRequireConfig, context.ToVoidPtr());
        ulong reference = state.ReferenceTracker.TrackAndPopRef(state.L, -1);
        return new LuauFunction(state, reference);
    }

    private static bool IsScriptModulePath(string path) =>
        path.StartsWith("./", StringComparison.Ordinal)
        || path.StartsWith("../", StringComparison.Ordinal)
        || path.StartsWith('@');

    private static bool IsInvalidScriptModulePath(string path) =>
        path.Contains('/', StringComparison.Ordinal) || path.Contains('\\', StringComparison.Ordinal);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int ResolveRequire(lua_State* L)
    {
        Context context = Context.FromUpvalue(L);
        if (!LuauStateMarshal.TryGetString(L, 1, out ReadOnlySpan<byte> utf8ModuleName))
        {
            lua_pushinteger(L, (int)Context.RequireResolutionKind.Error);
            LuauStateMarshal.PushString(L, "bad argument #1 to 'require' (string expected)");
            return 2;
        }

        string moduleName = Encoding.UTF8.GetString(utf8ModuleName);
        Context.RequireResolution resolution = context.ResolveRequire(moduleName);
        context.PushResolution(L, resolution);

        return resolution.Kind is Context.RequireResolutionKind.HostModule or Context.RequireResolutionKind.Error
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
        var context = Context.FromVoidPtr(ctx);
        Navigator navigator = context.Navigators[L];

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
        var context = Context.FromVoidPtr(ctx);
        Navigator navigator = context.Navigators[L];

        string strPath = new((sbyte*)path);
        if (!IsAbsolutePath(strPath))
            return luarequire_NavigateResult.NAVIGATE_NOT_FOUND;

        return navigator.ResetToPath(strPath);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_NavigateResult ToParent(lua_State* L, void* ctx)
    {
        var context = Context.FromVoidPtr(ctx);
        Navigator navigator = context.Navigators[L];

        return navigator.ToParent();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_NavigateResult ToChild(lua_State* L, void* ctx, byte* name)
    {
        var context = Context.FromVoidPtr(ctx);
        Navigator navigator = context.Navigators[L];

        string strName = new((sbyte*)name);
        return navigator.ToChild(strName);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static bool IsModulePresent(lua_State* L, void* ctx)
    {
        var context = Context.FromVoidPtr(ctx);
        Navigator navigator = context.Navigators[L];

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
        var context = Context.FromVoidPtr(ctx);
        Navigator navigator = context.Navigators[L];

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
        var context = Context.FromVoidPtr(ctx);
        Navigator navigator = context.Navigators[L];

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
        var context = Context.FromVoidPtr(ctx);
        Navigator navigator = context.Navigators[L];

        string strCacheKey = navigator.AbsoluteFilePath;
        return Write(strCacheKey, buffer, bufferSize, sizeOut);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static luarequire_ConfigStatus GetConfigStatus(lua_State* L, void* ctx)
    {
        var context = Context.FromVoidPtr(ctx);
        Navigator navigator = context.Navigators[L];

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
        var context = Context.FromVoidPtr(ctx);
        Navigator navigator = context.Navigators[L];

        string? strConfig = navigator.GetConfig();
        return Write(strConfig, buffer, bufferSize, sizeOut);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Load(lua_State* L, void* ctx, byte* path, byte* chunkname, byte* loadname)
    {
        string strPath = new((sbyte*)path);
        string strChunkName = new((sbyte*)chunkname);
        string strLoadName = new((sbyte*)loadname);

        var context = Context.FromVoidPtr(ctx);
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

    private static int ReportLoadError(lua_State* L, Context context, string strMsg)
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
        catch { }

        return null;
    }

    internal class Navigator
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

                return new ResolvedRealPath(luarequire_NavigateResult.NAVIGATE_SUCCESS)
                {
                    Path = strModulePath + strSuffix,
                };
            }
        }
    }

    internal class Navigators
    {
        private readonly Lock _dictLock = new();
        private readonly Dictionary<nint, Navigator> _dict = [];

        public Navigator this[lua_State* L]
        {
            get
            {
                lock (_dictLock)
                {
                    nint nKey = (nint)L;
                    if (!_dict.TryGetValue(nKey, out Navigator? nav))
                    {
                        nav = new Navigator();
                        _dict.Add(nKey, nav);
                    }
                    return nav;
                }
            }
        }
    }
}
