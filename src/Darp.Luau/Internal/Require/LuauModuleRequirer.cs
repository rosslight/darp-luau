using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Darp.Luau.Native;
using Darp.Luau.Utils;
using static Darp.Luau.Native.LuauNative;

[assembly: DisableRuntimeMarshalling]

namespace Darp.Luau.Internal.Require;

internal sealed unsafe class LuauModuleRequirer : IRequireContext, IDisposable
{
    private const int ContextUpvalueIndex = 1;

    private readonly LuauState _state;
    private readonly LuauHostModuleRequirer _hostModules;
    private readonly LuauScriptModuleRequirer _scriptModules;
    private GCHandle _handle;

    public LuauModuleRequirer(LuauState state)
    {
        _state = state;
        _hostModules = new LuauHostModuleRequirer(state);
        _scriptModules = new LuauScriptModuleRequirer(new VirtualFileSystem());
        _handle = GCHandle.Alloc(this);

        try
        {
            InstallRequireFunction();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public string? LoadError => _scriptModules.LoadError;

    public void EnableScriptModules()
    {
        _scriptModules.Enabled = true;
    }

    public void RegisterHostModule(string name, LuauState.OnModuleLoad onLoad)
    {
        _hostModules.RegisterModule(name, onLoad);
    }

    private void InstallRequireFunction()
    {
#if DEBUG
        using var guard = new StackGuard(_state.L, expectedDelta: 0);
#endif
        using LuauTable environment = _state.CreateEnvironment();
        using LuauFunction resolveRequire = CreateContextFunction(&ResolveRequire);
        using LuauFunction findCurrentFile = CreateContextFunction(&FindCurrentFile);
        using LuauFunction getScriptLoadError = CreateContextFunction(&GetScriptLoadError);
        using LuauFunction proxyRequire = _scriptModules.CreateProxyRequireFunction(_state);

        environment.Set("resolve_require", resolveRequire);
        environment.Set("find_current_file", findCurrentFile);
        environment.Set("get_script_load_error", getScriptLoadError);
        environment.Set("proxyrequire", proxyRequire);

        using LuauFunction require = _state
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
                  local ok, result = pcall(proxyrequire, path, find_current_file())
                  if ok then
                    return result
                  end

                  local load_error = get_script_load_error()
                  if load_error ~= nil and load_error ~= "" then
                    error(load_error)
                  end

                  error(result)
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

        _state.Globals.Set("require", require);
    }

    private LuauFunction CreateContextFunction(delegate* unmanaged[Cdecl]<lua_State*, int> callback)
    {
#if DEBUG
        using var guard = new StackGuard(_state.L, expectedDelta: 0);
#endif
        lua_pushlightuserdata(_state.L, ToVoidPtr());
        lua_pushcclosure(_state.L, callback, null, ContextUpvalueIndex);
        ulong reference = _state.ReferenceTracker.TrackAndPopRef(_state.L, -1);
        return new LuauFunction(_state, reference);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int ResolveRequire(lua_State* L)
    {
        LuauModuleRequirer requirer = FromUpvalue(L);
        if (!LuauStateMarshal.TryGetString(L, 1, out ReadOnlySpan<byte> utf8ModuleName))
        {
            lua_pushinteger(L, (int)RequireResolutionKind.Error);
            LuauStateMarshal.PushString(L, "bad argument #1 to 'require' (string expected)");
            return 2;
        }

        string moduleName = Encoding.UTF8.GetString(utf8ModuleName);
        RequireResolution resolution = requirer.Resolve(moduleName);
        requirer.PushResolution(L, resolution);

        return resolution.Kind is RequireResolutionKind.HostModule or RequireResolutionKind.Error ? 2 : 1;
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

            if (
                (debug.what is null || !NullTerminatedEquals(debug.what, "C"u8))
                && (debug.source is null || !NullTerminatedEquals(debug.source, "=darp_require"u8))
            )
            {
                lua_pushstring(L, debug.source);
                return 1;
            }
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetScriptLoadError(lua_State* L)
    {
        LuauModuleRequirer requirer = FromUpvalue(L);
        if (requirer._scriptModules.LoadError is { } loadError)
            LuauStateMarshal.PushString(L, loadError);
        else
            lua_pushnil(L);

        return 1;
    }

    private RequireResolution Resolve(string moduleName)
    {
        RequireResolution hostResolution = _hostModules.Resolve(moduleName);
        if (hostResolution.Kind != RequireResolutionKind.NotFound)
            return hostResolution;

        return _scriptModules.Resolve(moduleName);
    }

    private void PushResolution(lua_State* L, RequireResolution resolution)
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

    private void PushModule(lua_State* L, LuauTable module)
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

    public void Dispose()
    {
        _hostModules.Dispose();
        _scriptModules.Dispose();

        if (_handle.IsAllocated)
            _handle.Free();
    }

    private void* ToVoidPtr()
    {
        return (void*)GCHandle.ToIntPtr(_handle);
    }

    private static LuauModuleRequirer FromUpvalue(lua_State* L)
    {
        void* context = lua_tolightuserdata(L, unchecked((int)lua_upvalueindex(ContextUpvalueIndex)));
        return FromVoidPtr(context);
    }

    private static LuauModuleRequirer FromVoidPtr(void* context)
    {
        var handle = GCHandle.FromIntPtr((IntPtr)context);
        return handle.Target as LuauModuleRequirer ?? throw new ArgumentException("invalid context pointer");
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
}

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
