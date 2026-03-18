using System.Diagnostics.CodeAnalysis;
using Darp.Luau.Native;
using Darp.Luau.Utils;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau.Internal;

internal static unsafe class LuauTableAccessCore
{
    internal static void Set<T>(scoped in T source, in IntoLuau key, in IntoLuau value)
        where T : IReferenceSource, allows ref struct
    {
        LuauState state = source.Validate();
        if (key.Type is IntoLuau.Kind.Nil)
            throw new ArgumentNullException(nameof(key), "Cannot set a table value with nil key");
        lua_State* L = state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        using PopDisposable _ = source.PushToTop();
        key.Push(state);
        value.Push(state);
        lua_settable(L, -3);
    }

    internal static bool ContainsKey<T>(scoped in T source, in IntoLuau key)
        where T : IReferenceSource, allows ref struct
    {
        LuauState state = source.Validate();
        if (key.Type is IntoLuau.Kind.Nil)
            throw new ArgumentNullException(nameof(key), "Cannot set a table value with nil key");
        lua_State* L = state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        using PopDisposable _ = source.PushToTop();
        key.Push(state);
        var actualType = (lua_Type)lua_gettable(L, -2);
        bool hasValue = actualType != lua_Type.LUA_TNIL;
        lua_pop(L, 1);
        return hasValue;
    }

    internal static int ListCount<T>(scoped in T source)
        where T : IReferenceSource, allows ref struct
    {
        LuauState state = source.Validate();
        lua_State* L = state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif

        using PopDisposable _ = source.PushToStack(out int stackIndex);
        return lua_objlen(L, stackIndex);
    }

    public static bool TryGetRequired<T>(
        scoped in T source,
        in IntoLuau key,
        lua_Type expectedType,
        out lua_State* L,
        [NotNullWhen(false)] out string? error
    )
        where T : IReferenceSource, allows ref struct
    {
        L = null;
        error = null;

        LuauState state = source.Validate();
        L = state.L;
        _ = source.PushToTop();
        key.Push(state);

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

    public static bool TryGetOptional<T>(
        scoped in T source,
        in IntoLuau key,
        lua_Type expectedType,
        out lua_State* L,
        out bool isNil,
        [NotNullWhen(false)] out string? error
    )
        where T : IReferenceSource, allows ref struct
    {
        L = null;
        isNil = false;
        error = null;

        LuauState state = source.Validate();
        L = state.L;
        _ = source.PushToTop();
        key.Push(state);

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
}
