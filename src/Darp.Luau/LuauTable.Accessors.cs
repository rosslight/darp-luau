using System.Diagnostics.CodeAnalysis;
using Darp.Luau.Native;
using Darp.Luau.Utils;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau;

public unsafe partial struct LuauTable
{
    private readonly bool TryGetNumber(IntoLuau key, out double value, [NotNullWhen(false)] out string? error)
    {
#if DEBUG
        using var guard = new StackGuard(State!.L, expectedDelta: 0);
#endif
        value = 0;
        if (!TryGetRequired(key, lua_Type.LUA_TNUMBER, out lua_State* L, out error))
            return false;
        value = lua_tonumber(L, -1);
        lua_pop(L, 2);
        return true;
    }

    private readonly bool TryGetNumberOrNil(IntoLuau key, out double? value, [NotNullWhen(false)] out string? error)
    {
#if DEBUG
        using var guard = new StackGuard(State!.L, expectedDelta: 0);
#endif
        value = null;
        if (!TryGetOptional(key, lua_Type.LUA_TNUMBER, out lua_State* L, out bool isNil, out error))
            return false;
        if (isNil)
            return true;

        value = lua_tonumber(L, -1);
        lua_pop(L, 2);
        return true;
    }

    private readonly bool TryGetBoolean(IntoLuau key, out bool value, [NotNullWhen(false)] out string? error)
    {
        value = false;
        if (!TryGetRequired(key, lua_Type.LUA_TBOOLEAN, out lua_State* L, out error))
            return false;
        value = lua_toboolean(L, -1) == 1;
        lua_pop(L, 2);
        return true;
    }

    private readonly bool TryGetBooleanOrNil(IntoLuau key, out bool? value, [NotNullWhen(false)] out string? error)
    {
#if DEBUG
        using var guard = new StackGuard(State!.L, expectedDelta: 0);
#endif
        value = null;
        if (!TryGetOptional(key, lua_Type.LUA_TBOOLEAN, out lua_State* L, out bool isNil, out error))
            return false;
        if (isNil)
            return true;

        value = lua_toboolean(L, -1) == 1;
        lua_pop(L, 2);
        return true;
    }

    private readonly bool TryGetUtf8String(
        IntoLuau key,
        out ReadOnlySpan<byte> value,
        [NotNullWhen(false)] out string? error
    )
    {
#if DEBUG
        using var guard = new StackGuard(State!.L, expectedDelta: 0);
#endif
        value = default;
        if (!TryGetRequired(key, lua_Type.LUA_TSTRING, out lua_State* L, out error))
            return false;
        nuint len = 0;
        byte* pStr = lua_tolstring(L, -1, &len);
        if (pStr is null)
        {
            error = "Table value returned a null string pointer.";
            lua_pop(L, 2);
            return false;
        }
        value = new ReadOnlySpan<byte>(pStr, checked((int)len));
        lua_pop(L, 2);
        return true;
    }

    private readonly bool TryGetUtf8StringOrNil(
        IntoLuau key,
        out ReadOnlySpan<byte> value,
        out bool isNil,
        [NotNullWhen(false)] out string? error
    )
    {
#if DEBUG
        using var guard = new StackGuard(State!.L, expectedDelta: 0);
#endif
        value = default;
        if (!TryGetOptional(key, lua_Type.LUA_TSTRING, out lua_State* L, out isNil, out error))
            return false;
        if (isNil)
            return true;

        nuint len = 0;
        byte* pStr = lua_tolstring(L, -1, &len);
        if (pStr is null)
        {
            error = "Table value returned a null string pointer.";
            lua_pop(L, 2);
            return false;
        }

        value = new ReadOnlySpan<byte>(pStr, checked((int)len));
        lua_pop(L, 2);
        return true;
    }

    private readonly bool TryGetBuffer(
        IntoLuau key,
        out ReadOnlySpan<byte> value,
        [NotNullWhen(false)] out string? error
    )
    {
#if DEBUG
        using var guard = new StackGuard(State!.L, expectedDelta: 0);
#endif
        value = default;
        if (!TryGetRequired(key, lua_Type.LUA_TBUFFER, out lua_State* L, out error))
            return false;

        nuint length = 0;
        void* pBuf = lua_tobuffer(L, -1, &length);
        if (pBuf is null)
        {
            error = "Table value returned a null buffer pointer.";
            lua_pop(L, 2);
            return false;
        }

        value = new ReadOnlySpan<byte>(pBuf, checked((int)length));
        lua_pop(L, 2);
        return true;
    }

    private readonly bool TryGetBufferOrNil(
        IntoLuau key,
        out ReadOnlySpan<byte> value,
        out bool isNil,
        [NotNullWhen(false)] out string? error
    )
    {
#if DEBUG
        using var guard = new StackGuard(State!.L, expectedDelta: 0);
#endif
        value = default;
        if (!TryGetOptional(key, lua_Type.LUA_TBUFFER, out lua_State* L, out isNil, out error))
            return false;
        if (isNil)
            return true;

        nuint length = 0;
        void* pBuf = lua_tobuffer(L, -1, &length);
        if (pBuf is null)
        {
            error = "Table value returned a null buffer pointer.";
            lua_pop(L, 2);
            return false;
        }

        value = new ReadOnlySpan<byte>(pBuf, checked((int)length));
        lua_pop(L, 2);
        return true;
    }

    private readonly bool TryGetUserdata<T>(
        IntoLuau key,
        [NotNullWhen(true)] out T? value,
        [NotNullWhen(false)] out string? error
    )
        where T : class, ILuauUserData<T>
    {
#if DEBUG
        using var guard = new StackGuard(State!.L, expectedDelta: 0);
#endif
        value = null;
        if (!TryGetRequired(key, lua_Type.LUA_TUSERDATA, out lua_State* L, out error))
            return false;

        bool ok = ManagedUserdataResolver.TryResolve(L, -1, out value, out error, valueLabel: "Table value");
        lua_pop(L, 2);
        return ok;
    }

    private readonly bool TryGetUserdataOrNil<T>(IntoLuau key, out T? value, [NotNullWhen(false)] out string? error)
        where T : class, ILuauUserData<T>
    {
#if DEBUG
        using var guard = new StackGuard(State!.L, expectedDelta: 0);
#endif
        value = null;
        if (!TryGetOptional(key, lua_Type.LUA_TUSERDATA, out lua_State* L, out bool isNil, out error))
            return false;
        if (isNil)
            return true;

        bool ok = ManagedUserdataResolver.TryResolve(L, -1, out value, out error, valueLabel: "Table value");
        lua_pop(L, 2);
        return ok;
    }

    private readonly bool TryGetLuauValue(IntoLuau key, out LuauValue value, [NotNullWhen(false)] out string? error)
    {
#if DEBUG
        using var guard = new StackGuard(State!.L, expectedDelta: 0);
#endif
        value = default;
        TryGet(key, out lua_State* L, out error);

        value = LuauValue.ToValue(State);
        lua_pop(L, 2);
        return true;
    }

    private readonly bool TryGetLuauTable(IntoLuau key, out LuauTable value, [NotNullWhen(false)] out string? error)
    {
#if DEBUG
        using var guard = new StackGuard(State!.L, expectedDelta: 0);
#endif
        value = default;
        if (!TryGetRequired(key, lua_Type.LUA_TTABLE, out lua_State* L, out error))
            return false;

        value = new LuauTable(State, lua_ref(L, -1));
        lua_pop(L, 2);
        return true;
    }

    private readonly bool TryGetLuauFunction(
        IntoLuau key,
        out LuauFunction value,
        [NotNullWhen(false)] out string? error
    )
    {
#if DEBUG
        using var guard = new StackGuard(State!.L, expectedDelta: 0);
#endif
        value = default;
        if (!TryGetRequired(key, lua_Type.LUA_TFUNCTION, out lua_State* L, out error))
            return false;

        value = new LuauFunction(State, lua_ref(L, -1));
        lua_pop(L, 2);
        return true;
    }

    private readonly bool TryGetLuauString(IntoLuau key, out LuauString value, [NotNullWhen(false)] out string? error)
    {
#if DEBUG
        using var guard = new StackGuard(State!.L, expectedDelta: 0);
#endif
        value = default;
        if (!TryGetRequired(key, lua_Type.LUA_TSTRING, out lua_State* L, out error))
            return false;

        value = new LuauString(State, lua_ref(L, -1));
        lua_pop(L, 2);
        return true;
    }

    private readonly bool TryGetLuauBuffer(IntoLuau key, out LuauBuffer value, [NotNullWhen(false)] out string? error)
    {
#if DEBUG
        using var guard = new StackGuard(State!.L, expectedDelta: 0);
#endif
        value = default;
        if (!TryGetRequired(key, lua_Type.LUA_TBUFFER, out lua_State* L, out error))
            return false;

        value = new LuauBuffer(State, lua_ref(L, -1));
        lua_pop(L, 2);
        return true;
    }

    private readonly bool TryGetLuauUserdata(
        IntoLuau key,
        out LuauUserdata value,
        [NotNullWhen(false)] out string? error
    )
    {
#if DEBUG
        using var guard = new StackGuard(State!.L, expectedDelta: 0);
#endif
        value = default;
        if (!TryGetRequired(key, lua_Type.LUA_TUSERDATA, out lua_State* L, out error))
            return false;

        value = new LuauUserdata(State, lua_ref(L, -1));
        lua_pop(L, 2);
        return true;
    }

    private readonly void TryGet(IntoLuau key, out lua_State* L, [NotNullWhen(false)] out string? error)
    {
        L = null;
        error = null;

        State.ThrowIfDisposed();
        L = State.L;
        lua_getref(L, Reference);
        key.Push(State);
        _ = (lua_Type)lua_gettable(L, -2);
    }

    private readonly bool TryGetRequired(
        IntoLuau key,
        lua_Type expectedType,
        out lua_State* L,
        [NotNullWhen(false)] out string? error
    )
    {
        L = null;
        error = null;

        State.ThrowIfDisposed();
        L = State.L;
        lua_getref(L, Reference);
        key.Push(State);

        var actualType = (lua_Type)lua_gettable(L, -2);
        if (actualType == expectedType)
        {
            return true;
        }

        error =
            actualType == lua_Type.LUA_TNIL
                ? $"Table value is nil but {expectedType} is required."
                : $"Table value must be {expectedType} but was {actualType}.";
        lua_pop(L, 2);
        return false;
    }

    private readonly bool TryGetOptional(
        IntoLuau key,
        lua_Type expectedType,
        out lua_State* L,
        out bool isNil,
        [NotNullWhen(false)] out string? error
    )
    {
        L = null;
        isNil = false;
        error = null;

        ThrowIfDisposed();
        L = State.L;
        lua_getref(L, Reference);
        key.Push(State);

        var actualType = (lua_Type)lua_gettable(L, -2);
        if (actualType == lua_Type.LUA_TNIL)
        {
            isNil = true;
            lua_pop(L, 2);
            return true;
        }

        if (actualType == expectedType)
            return true;

        error = $"Table value must be {expectedType} or {lua_Type.LUA_TNIL} but was {actualType}.";
        lua_pop(L, 2);
        return false;
    }

    private static LuaGetException CreateReadException(string error) => new(error);
}
