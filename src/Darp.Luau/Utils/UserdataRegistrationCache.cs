using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Darp.Luau.Native;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau.Utils;

internal sealed class UserdataRegistrationCache(LuauState state) : IDisposable
{
    private readonly LuauState _state = state;
    private readonly Dictionary<Type, GCHandle> _registrations = [];
    private readonly ConditionalWeakTable<object, ObjectIdentity> _identityByUserdata = new();
    private readonly int _identityMapReference = CreateIdentityMapReference(state);
    private int _nextIdentity;
    private bool _userdataCallbacksRegistered;

    private sealed class ObjectIdentity(int value)
    {
        public int Value { get; } = value;
    }

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
            ReadOnlySpan<char> resolvedMemberName = memberName[..memberNameLength];

            int callbackFrameToken = state.EnterCallbackFrame();
            try
            {
                LuauReturnSingle result = T.OnIndex(target, state, resolvedMemberName);

                if (result.TryPushValue(state, out string? error))
                    return 1;
                if (error == LuauReturn.NotHandled)
                    return 0;
                return error;
            }
            finally
            {
                state.ExitCallbackFrame(callbackFrameToken);
            }
        }

        static LuaResult<int, string> NewIndexCallbackManaged(LuauState lua, object? userdata)
        {
            lua_State* L = lua.L;
            if (userdata is not T target)
                return $"Expected userdata of type '{typeof(T).FullName}'.";

            if (!LuauStateMarshal.TryGetString(L, 2, out ReadOnlySpan<byte> utf8MemberName))
                return "userdata assignment requires a string member name";

            Span<char> memberName = stackalloc char[Encoding.UTF8.GetCharCount(utf8MemberName)];
            int memberNameLength = Encoding.UTF8.GetChars(utf8MemberName, memberName);
            ReadOnlySpan<char> resolvedMemberName = memberName[..memberNameLength];

            int callbackFrameToken = lua.EnterCallbackFrame();
            try
            {
                var args = new LuauArgs(lua, argumentCount: 1, firstParameterStackIndex: 3, callbackFrameToken);
                Debug.Assert(args.ArgumentCount == 1);
                var argsSingle = new LuauArgsSingle(args);
                LuauOutcome result = T.OnSetIndex(target, argsSingle, resolvedMemberName);
                if (!result.TryGetError(out string? error))
                {
                    // Success
                    return 0;
                }
                if (error == LuauReturn.NotHandled)
                    return (string)$"attempt to set unknown userdata member '{resolvedMemberName}'";
                return error;
            }
            finally
            {
                lua.ExitCallbackFrame(callbackFrameToken);
            }
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
                numberOfParameters = Math.Max(0, lua_gettop(L) - 1);
                firstParameterStackIndex = 2;
            }
            else if (LuauStateMarshal.TryGetString(L, 2, out utf8MethodName))
            {
                numberOfParameters = Math.Max(0, lua_gettop(L) - 2);
                firstParameterStackIndex = 3;
            }
            else
            {
                return "userdata method call requires a string method name";
            }

            int topBeforeInvoke = lua_gettop(L);
            int callbackFrameToken = lua.EnterCallbackFrame();
            var functionArgs = new LuauArgs(lua, numberOfParameters, firstParameterStackIndex, callbackFrameToken);
            Span<char> methodName = stackalloc char[Encoding.UTF8.GetCharCount(utf8MethodName)];
            int memberNameLength = Encoding.UTF8.GetChars(utf8MethodName, methodName);
            ReadOnlySpan<char> resolvedMethodName = methodName[..memberNameLength];
            try
            {
                LuauReturn result = T.OnMethodCall(target, functionArgs, resolvedMethodName);
                lua_settop(L, topBeforeInvoke);

                if (result.TryPushValues(lua, out int outputCount, out string? error))
                    return outputCount;
                if (error == LuauReturn.NotHandled)
                    return (string)$"attempt to call unknown userdata method '{resolvedMethodName}'";
                return error;
            }
            catch
            {
                lua_settop(L, topBeforeInvoke);
                throw;
            }
            finally
            {
                lua.ExitCallbackFrame(callbackFrameToken);
            }
        }
    }

    public unsafe LuauUserdata GetOrCreate<T>(T userdata)
        where T : class, ILuauUserData<T>
    {
        EnsureUserdataCallbacksRegistered();

        int identity = _identityByUserdata.GetValue(userdata, _ => new ObjectIdentity(++_nextIdentity)).Value;
        lua_State* L = _state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif

        if (TryGetCachedUserdataReference(L, identity, out int cachedReference))
        {
            return new LuauUserdata(_state, cachedReference);
        }

        GCHandle registrationHandle = Register<T>();
        var ptr = (LuauUserdataNative*)lua_newuserdatataggedwithmetatable(
            L,
            (nuint)sizeof(LuauUserdataNative),
            LuauUserdataNative.Tag
        );
        ptr->UserdataHandle = GCHandle.Alloc(userdata, GCHandleType.Normal);
        ptr->RegistryValueHandle = registrationHandle;

        // stack: [userdata]
        lua_getref(L, _identityMapReference); // [userdata, identityMap]
        lua_pushinteger(L, identity); // [userdata, identityMap, identity]
        lua_pushvalue(L, -3); // [userdata, identityMap, identity, userdata]
        lua_settable(L, -3); // [userdata, identityMap]
        lua_pop(L, 1); // [userdata]

        int reference = _state.ReferenceTracker.TrackAndPopRef(L, -1);
        return new LuauUserdata(_state, reference);
    }

    public unsafe void Dispose()
    {
        if (!_state.IsDisposed && _identityMapReference != 0)
            lua_unref(_state.L, _identityMapReference);

        foreach (GCHandle handle in _registrations.Values)
        {
            if (handle.IsAllocated)
                handle.Free();
        }
        _registrations.Clear();
    }

    private static unsafe int CreateIdentityMapReference(LuauState state)
    {
        lua_State* L = state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        lua_newtable(L); // cache
        lua_newtable(L); // metatable

        fixed (byte* pModeName = "__mode\0"u8)
        fixed (byte* pWeakValues = "v"u8)
        {
            lua_pushlstring(L, pWeakValues, 1);
            lua_setfield(L, -2, pModeName);
        }

        _ = lua_setmetatable(L, -2);
        return LuauNativeMethods.luaL_ref(L, LUA_REGISTRYINDEX);
    }

    private unsafe bool TryGetCachedUserdataReference(lua_State* L, int identity, out int reference)
    {
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        lua_getref(L, _identityMapReference); // [cache]
        lua_pushinteger(L, identity); // [cache, identity]
        _ = lua_gettable(L, -2); // [cache, value]

        if ((lua_Type)lua_type(L, -1) is not lua_Type.LUA_TUSERDATA)
        {
            reference = 0;
            lua_pop(L, 2);
            return false;
        }

        var native = (LuauUserdataNative*)lua_touserdatatagged(L, -1, LuauUserdataNative.Tag);
        if (native is null || !native->UserdataHandle.IsAllocated)
        {
            reference = 0;
            lua_pop(L, 2);
            return false;
        }

        reference = _state.ReferenceTracker.TrackRef(L, -1);
        lua_pop(L, 2);
        return true;
    }

    private unsafe void EnsureUserdataCallbacksRegistered()
    {
        if (_userdataCallbacksRegistered)
            return;

        lua_State* L = _state.L;
        int initialTop = lua_gettop(L);
        try
        {
            lua_newtable(L);

            fixed (byte* pIndexName = "__index\0"u8)
            {
                _state.PushWrappedCallback(&IndexCallback, pIndexName);
                lua_setfield(L, -2, pIndexName);
            }
            fixed (byte* pNewIndexName = "__newindex\0"u8)
            {
                _state.PushWrappedCallback(&NewIndexCallback, pNewIndexName);
                lua_setfield(L, -2, pNewIndexName);
            }
            fixed (byte* pNameCallName = "__namecall\0"u8)
            {
                _state.PushWrappedCallback(&MethodCallback, pNameCallName);
                lua_setfield(L, -2, pNameCallName);
            }

            lua_setuserdatametatable(L, LuauUserdataNative.Tag);
            lua_setuserdatadtor(L, LuauUserdataNative.Tag, &UserdataDestructor);
            _userdataCallbacksRegistered = true;
        }
        finally
        {
            lua_settop(L, initialTop);
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

        var native = (LuauUserdataNative*)lua_touserdatatagged(L, 1, LuauUserdataNative.Tag);
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
        if (!native->UserdataHandle.IsAllocated)
        {
            errorMessage = "userdata handle is not allocated"u8;
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
