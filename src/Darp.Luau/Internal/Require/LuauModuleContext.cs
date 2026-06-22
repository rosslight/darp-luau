using System.Runtime.InteropServices;
using Darp.Luau.Native;
using Darp.Luau.Utils;

namespace Darp.Luau.Internal.Require;

/// <summary>internal require context</summary>
internal sealed class LuauModuleContext : IDisposable, IRequireContext
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

        public static RequireResolution ScriptModule { get; } = new(RequireResolutionKind.ScriptModule, default, null);

        public static RequireResolution LoadedHostModule(LuauTable module) =>
            new(RequireResolutionKind.HostModule, module, null);

        public static RequireResolution LoadError(string error) => new(RequireResolutionKind.Error, default, error);
    }

    private delegate RequireResolution RequireResolver(LuauModuleContext context, string moduleName);

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

    internal LuauModuleContext(LuauState state)
    {
        _state = state;
        _handle = GCHandle.Alloc(this);
        _resolvers = [ResolveHostModule, ResolveScriptModule];
    }

    internal LuauModuleNavigators Navigators { get; } = new();

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
            LuauNative.lua_xmove(_state.L, L, 1);
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

    internal unsafe void PushResolution(lua_State* L, RequireResolution resolution)
    {
        LuauNative.lua_pushinteger(L, (int)resolution.Kind);
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

    private static RequireResolution ResolveHostModule(LuauModuleContext context, string name)
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

    private static RequireResolution ResolveScriptModule(LuauModuleContext context, string path)
    {
        if (LuauRequireByString.IsScriptModulePath(path))
        {
            return context.ScriptModulesEnabled
                ? RequireResolution.ScriptModule
                : RequireResolution.LoadError("script module require is not enabled");
        }

        return LuauRequireByString.IsInvalidScriptModulePath(path)
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

    internal unsafe void* ToVoidPtr()
    {
        return (void*)GCHandle.ToIntPtr(_handle);
    }

    internal static unsafe LuauModuleContext FromVoidPtr(void* pCtx)
    {
        var gchCtx = GCHandle.FromIntPtr((IntPtr)pCtx);
        return gchCtx.Target as LuauModuleContext ?? throw new ArgumentException("invalid context pointer");
    }

    internal static unsafe LuauModuleContext FromUpvalue(lua_State* L)
    {
        void* pCtx = LuauNative.lua_tolightuserdata(L, unchecked((int)LuauNative.lua_upvalueindex(1)));
        return FromVoidPtr(pCtx);
    }
}
