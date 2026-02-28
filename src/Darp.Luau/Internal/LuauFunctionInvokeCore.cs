using Darp.Luau.Native;
using Darp.Luau.Utils;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau.Internal;

internal static unsafe class LuauFunctionInvokeCore
{
    internal static TR Invoke0<TR>(scoped in LuauRefSource source, ReadOnlySpan<char> ownerTypeName)
        where TR : allows ref struct
    {
        LuauState state = source.Validate(ownerTypeName);
        lua_State* L = state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        source.Push(L, ownerTypeName);
        return InvokeAfterPush<TR>(state, L, nargs: 0);
    }

    internal static TR Invoke1<TR>(scoped in LuauRefSource source, in IntoLuau p1, ReadOnlySpan<char> ownerTypeName)
        where TR : allows ref struct
    {
        LuauState state = source.Validate(ownerTypeName);
        lua_State* L = state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        source.Push(L, ownerTypeName);
        p1.Push(state);
        return InvokeAfterPush<TR>(state, L, nargs: 1);
    }

    internal static TR Invoke2<TR>(
        scoped in LuauRefSource source,
        in IntoLuau p1,
        in IntoLuau p2,
        ReadOnlySpan<char> ownerTypeName
    )
        where TR : allows ref struct
    {
        LuauState state = source.Validate(ownerTypeName);
        lua_State* L = state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        source.Push(L, ownerTypeName);
        p1.Push(state);
        p2.Push(state);
        return InvokeAfterPush<TR>(state, L, nargs: 2);
    }

    private static TR InvokeAfterPush<TR>(LuauState state, lua_State* L, int nargs)
        where TR : allows ref struct
    {
        int nresults = typeof(TR) == typeof(LuauNil) ? 0 : 1;
        int status = lua_pcall(L, nargs, nresults, 0);
        LuaException.ThrowIfNotOk(L, status, "lua_pcall");

        if (nresults == 0)
            return default!;

        using var luaReturn = LuauValue.ToValue(state);
        lua_pop(L, 1);
        if (luaReturn.TryGet(out TR? result, acceptNil: true))
            return result;

        throw new InvalidCastException(
            $"Could not convert Lua return value of type '{luaReturn.Type}' to '{typeof(TR).FullName}'."
        );
    }
}
