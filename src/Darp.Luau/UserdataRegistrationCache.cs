using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Darp.Luau.Native;

namespace Darp.Luau;

internal sealed class UserdataRegistrationCache(LuauState state) : IDisposable
{
    private readonly LuauState _state = state;
    private readonly Dictionary<Type, GCHandle> _registrations = [];
    private bool _userdataCallbacksRegistered;

    public unsafe GCHandle Register<T>()
        where T : class, ILuauUserData<T>
    {
        if (_registrations.TryGetValue(typeof(T), out GCHandle handle))
            return handle;

        EnsureUserdataCallbacksRegistered();

        var callbackRegistration = new UserdataCallbackRegistration(
            _state,
            IndexCallbackManaged,
            NewIndexCallbackManaged,
            MethodCallbackManaged
        );
        handle = GCHandle.Alloc(callbackRegistration);
        _registrations.Add(typeof(T), handle);
        return handle;

        static LuaResult<int, string> IndexCallbackManaged(LuauState state, object? userdata)
        {
            lua_State* L = state.L;
            if (userdata is not T target)
                return $"Expected userdata of type '{typeof(T).FullName}'.";

            if (!LuauStateMarshal.TryGetString(L, 2, out ReadOnlySpan<byte> utf8MemberName))
                return "userdata index access requires a string member name";

            Span<char> memberName = stackalloc char[Encoding.UTF8.GetCharCount(utf8MemberName)];
            int memberNameLength = Encoding.UTF8.GetChars(utf8MemberName, memberName);
            if (!T.OnIndex(target, state, memberName[..memberNameLength], out IntoLuau result))
                return 0;

            result.Push(state);
            return 1;
        }

        static LuaResult<int, string> NewIndexCallbackManaged(LuauState lua, object? userdata)
        {
            lua_State* L = lua.L;
            if (userdata is not T target)
                return $"Expected userdata of type '{typeof(T).FullName}'.";

            if (!LuauStateMarshal.TryGetString(L, 2, out ReadOnlySpan<byte> utf8MemberName))
                return "userdata assignment requires a string member name";

            var valueView = new LuauView(lua, stackIndex: 3);
            Span<char> memberName = stackalloc char[Encoding.UTF8.GetCharCount(utf8MemberName)];
            int memberNameLength = Encoding.UTF8.GetChars(utf8MemberName, memberName);
            if (!T.OnSetIndex(target, valueView, memberName[..memberNameLength]))
                return (string)$"attempt to set unknown userdata member '{memberName}'";
            return 0;
        }

        static LuaResult<int, string> MethodCallbackManaged(LuauState lua, object? userdata)
        {
            lua_State* L = lua.L;
            if (userdata is not T target)
                return $"Expected userdata of type '{typeof(T).FullName}'.";

            int firstParameterStackIndex;
            int numberOfParameters;
            if (LuauStateMarshal.TryGetNameCall(L, out ReadOnlySpan<byte> utf8MethodName))
            {
                numberOfParameters = Math.Max(0, LuauNative.lua_gettop(L) - 1);
                firstParameterStackIndex = 2;
            }
            else if (LuauStateMarshal.TryGetString(L, 2, out utf8MethodName))
            {
                numberOfParameters = Math.Max(0, LuauNative.lua_gettop(L) - 2);
                firstParameterStackIndex = 3;
            }
            else
            {
                return "userdata method call requires a string method name";
            }

            int topBeforeInvoke = LuauNative.lua_gettop(L);
            var functionArgs = new LuauFunctions(lua, numberOfParameters, firstParameterStackIndex);
            Span<char> methodName = stackalloc char[Encoding.UTF8.GetCharCount(utf8MethodName)];
            int memberNameLength = Encoding.UTF8.GetChars(utf8MethodName, methodName);
            bool handled = T.OnMethodCall(target, functionArgs, methodName[..memberNameLength]);
            int outputCount = LuauNative.lua_gettop(L) - topBeforeInvoke;

            if (handled)
                return Math.Max(0, outputCount);

            if (outputCount > 0)
                LuauNative.lua_pop(L, outputCount);

            return (string)$"attempt to call unknown userdata method '{methodName}'";
        }
    }

    public void Dispose()
    {
        foreach (GCHandle handle in _registrations.Values)
        {
            if (handle.IsAllocated)
                handle.Free();
        }
        _registrations.Clear();
    }

    private unsafe void EnsureUserdataCallbacksRegistered()
    {
        if (_userdataCallbacksRegistered)
            return;

        lua_State* L = _state.L;
        int initialTop = LuauNative.lua_gettop(L);
        try
        {
            LuauNative.lua_newtable(L);

            fixed (byte* pIndexName = "__index\0"u8)
            {
                _state.PushWrappedCallback(&IndexCallback, pIndexName);
                LuauNative.lua_setfield(L, -2, pIndexName);
            }
            fixed (byte* pNewIndexName = "__newindex\0"u8)
            {
                _state.PushWrappedCallback(&NewIndexCallback, pNewIndexName);
                LuauNative.lua_setfield(L, -2, pNewIndexName);
            }
            fixed (byte* pNameCallName = "__namecall\0"u8)
            {
                _state.PushWrappedCallback(&MethodCallback, pNameCallName);
                LuauNative.lua_setfield(L, -2, pNameCallName);
            }

            LuauNative.lua_setuserdatametatable(L, LuauUserdataNative.Tag);
            LuauNative.lua_setuserdatadtor(L, LuauUserdataNative.Tag, &UserdataDestructor);
            _userdataCallbacksRegistered = true;
        }
        finally
        {
            LuauNative.lua_settop(L, initialTop);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void UserdataDestructor(lua_State* _, void* pUserdata)
    {
        if (pUserdata is null)
            return;
        var native = (LuauUserdataNative*)pUserdata;
        if (native->UserdataHandle.IsAllocated)
            native->UserdataHandle.Free();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe int IndexCallback(lua_State* L)
    {
        try
        {
            if (!TryGetCallbackRegistration(L, out var registration, out object? userdata, out var errorMessage))
                return LuauStateMarshal.ReturnError(L, errorMessage);
            LuaResult<int, string> result = registration.OnIndexCallback(registration.State, userdata);
            return LuauStateMarshal.ReturnResult(L, result);
        }
        catch (Exception exception)
        {
            return LuauStateMarshal.ReturnCallbackException(L, "__index", exception);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe int NewIndexCallback(lua_State* L)
    {
        try
        {
            if (!TryGetCallbackRegistration(L, out var registration, out object? userdata, out var errorMessage))
                return LuauStateMarshal.ReturnError(L, errorMessage);
            LuaResult<int, string> result = registration.OnNewIndexCallback(registration.State, userdata);
            return LuauStateMarshal.ReturnResult(L, result);
        }
        catch (Exception exception)
        {
            return LuauStateMarshal.ReturnCallbackException(L, "__newindex", exception);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe int MethodCallback(lua_State* L)
    {
        try
        {
            if (!TryGetCallbackRegistration(L, out var registration, out object? userdata, out var errorMessage))
                return LuauStateMarshal.ReturnError(L, errorMessage);
            LuaResult<int, string> result = registration.OnMethodCallback(registration.State, userdata);
            return LuauStateMarshal.ReturnResult(L, result);
        }
        catch (Exception exception)
        {
            return LuauStateMarshal.ReturnCallbackException(L, "__namecall", exception);
        }
    }

    private static unsafe bool TryGetCallbackRegistration(
        lua_State* L,
        [NotNullWhen(true)] out UserdataCallbackRegistration? callbackRegistration,
        out object? userdata,
        out ReadOnlySpan<byte> errorMessage
    )
    {
        callbackRegistration = null!;
        userdata = null;
        errorMessage = default;

        var native = (LuauUserdataNative*)LuauNative.lua_touserdatatagged(L, 1, LuauUserdataNative.Tag);
        if (native is null)
        {
            errorMessage = "invalid userdata value"u8;
            return false;
        }
        if (!native->RegistryValueHandle.IsAllocated)
        {
            errorMessage = "userdata callback registration is not allocated"u8;
            return false;
        }
        if (native->RegistryValueHandle.Target is not UserdataCallbackRegistration resolvedRegistration)
        {
            errorMessage = "userdata callback registration is invalid"u8;
            return false;
        }
        if (resolvedRegistration.State.L != L)
        {
            errorMessage = "userdata callback registration belongs to a different state"u8;
            return false;
        }

        userdata = native->UserdataHandle.Target;
        callbackRegistration = resolvedRegistration;
        return true;
    }

    private sealed record UserdataCallbackRegistration(
        LuauState State,
        UserdataCallbackRegistration.OnLuaCallback OnIndexCallback,
        UserdataCallbackRegistration.OnLuaCallback OnNewIndexCallback,
        UserdataCallbackRegistration.OnLuaCallback OnMethodCallback
    )
    {
        public delegate LuaResult<int, string> OnLuaCallback(LuauState lua, object? userdata);
    }
}

internal readonly ref struct LuaResult<T, TError>
    where T : allows ref struct
    where TError : allows ref struct
{
    private readonly T? _value;
    private readonly TError? _error;

    public bool IsOk { get; }

    private LuaResult(bool isOk, T? value, TError? error)
    {
        IsOk = isOk;
        _value = value;
        _error = error;
    }

    public bool TryGetValue([NotNullWhen(true)] out T? value, [NotNullWhen(false)] out TError? error)
    {
        if (IsOk)
        {
            value = _value!;
            error = _error;
            return true;
        }
        value = _value;
        error = _error!;
        return false;
    }

    public static implicit operator LuaResult<T, TError>(T value) => new(true, value, default);

    public static implicit operator LuaResult<T, TError>(TError error) => new(false, default, error);
}
