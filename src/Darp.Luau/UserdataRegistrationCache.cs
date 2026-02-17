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

        static int IndexCallbackManaged(LuauState state, object? userdata)
        {
            lua_State* L = state.L;
            try
            {
                if (userdata is not T target)
                    return LuauStateMarshal.RaiseLuaError(L, $"Expected userdata of type '{typeof(T).FullName}'.");

                if (!TryGetString(L, 2, out ReadOnlySpan<byte> utf8MemberName))
                    return LuauStateMarshal.RaiseLuaError(L, "userdata index access requires a string member name"u8);

                Span<char> memberName = stackalloc char[Encoding.UTF8.GetCharCount(utf8MemberName)];
                int memberNameLength = Encoding.UTF8.GetChars(utf8MemberName, memberName);
                if (!T.OnIndex(target, state, memberName[..memberNameLength], out IntoLuau result))
                    return 0;

                result.Push(state);
                return 1;
            }
            catch (Exception exception)
            {
                return LuauStateMarshal.RaiseCallbackException(state.L, "__index", exception);
            }
        }

        static int NewIndexCallbackManaged(LuauState lua, object? userdata)
        {
            try
            {
                lua_State* L = lua.L;
                if (userdata is not T target)
                    return LuauStateMarshal.RaiseLuaError(L, $"Expected userdata of type '{typeof(T).FullName}'.");

                if (!TryGetString(L, 2, out ReadOnlySpan<byte> utf8MemberName))
                    return LuauStateMarshal.RaiseLuaError(L, "userdata assignment requires a string member name"u8);

                var valueView = new LuauView(lua, stackIndex: 3);
                Span<char> memberName = stackalloc char[Encoding.UTF8.GetCharCount(utf8MemberName)];
                int memberNameLength = Encoding.UTF8.GetChars(utf8MemberName, memberName);
                if (!T.OnSetIndex(target, valueView, memberName[..memberNameLength]))
                    return LuauStateMarshal.RaiseLuaError(L, $"attempt to set unknown userdata member '{memberName}'");
                return 0;
            }
            catch (Exception exception)
            {
                return LuauStateMarshal.RaiseCallbackException(lua.L, "__newindex", exception);
            }
        }

        static int MethodCallbackManaged(LuauState lua, object? userdata)
        {
            lua_State* L = lua.L;
            int initialTop = LuauNative.lua_gettop(L);
            try
            {
                if (userdata is not T target)
                    return LuauStateMarshal.RaiseLuaError(L, $"Expected userdata of type '{typeof(T).FullName}'.");

                int firstParameterStackIndex;
                int numberOfParameters;
                if (TryGetNameCall(L, out ReadOnlySpan<byte> utf8MethodName))
                {
                    numberOfParameters = Math.Max(0, LuauNative.lua_gettop(L) - 1);
                    firstParameterStackIndex = 2;
                }
                else if (TryGetString(L, 2, out utf8MethodName))
                {
                    numberOfParameters = Math.Max(0, LuauNative.lua_gettop(L) - 2);
                    firstParameterStackIndex = 3;
                }
                else
                {
                    return LuauStateMarshal.RaiseLuaError(L, "userdata method call requires a string method name");
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

                return LuauStateMarshal.RaiseLuaError(L, $"attempt to call unknown userdata method '{methodName}'");
            }
            catch (Exception exception)
            {
                LuauNative.lua_settop(L, initialTop);
                return LuauStateMarshal.RaiseCallbackException(lua.L, "__namecall", exception);
            }
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
                LuauNative.lua_pushcfunction(L, &IndexCallback, pIndexName);
                LuauNative.lua_setfield(L, -2, pIndexName);
            }

            fixed (byte* pNewIndexName = "__newindex\0"u8)
            {
                LuauNative.lua_pushcfunction(L, &NewIndexCallback, pNewIndexName);
                LuauNative.lua_setfield(L, -2, pNewIndexName);
            }

            fixed (byte* pNameCallName = "__namecall\0"u8)
            {
                LuauNative.lua_pushcfunction(L, &MethodCallback, pNameCallName);
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

    private static unsafe bool TryGetString(lua_State* L, int stackIndex, out ReadOnlySpan<byte> value)
    {
        if ((lua_Type)LuauNative.lua_type(L, stackIndex) is not lua_Type.LUA_TSTRING)
        {
            value = default;
            return false;
        }

        nuint keyLength;
        byte* keyUtf8 = LuauNative.lua_tolstring(L, stackIndex, &keyLength);
        if (keyUtf8 is null)
        {
            value = default;
            return false;
        }
        value = new ReadOnlySpan<byte>(keyUtf8, checked((int)keyLength));
        return true;
    }

    private static unsafe bool TryGetNameCall(lua_State* L, out ReadOnlySpan<byte> methodName)
    {
        int atom = 0;
        byte* name = LuauNative.lua_namecallatom(L, &atom);
        if (name is null)
        {
            methodName = default;
            return false;
        }

        int length = 0;
        while (name[length] != 0)
            length++;

        methodName = new ReadOnlySpan<byte>(name, length);
        return true;
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
        if (!TryGetCallbackRegistration(L, out var registration, out object? userdata, out var errorMessage))
            return LuauStateMarshal.RaiseLuaError(L, errorMessage);
        return registration.OnIndexCallback(registration.State, userdata);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe int NewIndexCallback(lua_State* L)
    {
        if (!TryGetCallbackRegistration(L, out var registration, out object? userdata, out var errorMessage))
            return LuauStateMarshal.RaiseLuaError(L, errorMessage);
        return registration.OnNewIndexCallback(registration.State, userdata);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe int MethodCallback(lua_State* L)
    {
        if (!TryGetCallbackRegistration(L, out var registration, out object? userdata, out var errorMessage))
            return LuauStateMarshal.RaiseLuaError(L, errorMessage);
        return registration.OnMethodCallback(registration.State, userdata);
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
        public delegate int OnLuaCallback(LuauState lua, object? userdata);
    }
}
