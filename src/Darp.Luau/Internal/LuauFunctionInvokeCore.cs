using Darp.Luau.Native;
using Darp.Luau.Utils;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau.Internal;

internal static unsafe class LuauFunctionInvokeCore
{
    internal static TR Invoke0<T, TR>(scoped in T? source)
        where T : IReferenceSource, allows ref struct
        where TR : allows ref struct
    {
        LuauState state = source.Validate();
        lua_State* L = state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        using var _ = source.PushToTop();
        return InvokeAfterPush<TR>(state, L, nargs: 0);
    }

    internal static TR Invoke1<T, TR>(scoped in T? source, in IntoLuau p1)
        where T : IReferenceSource, allows ref struct
        where TR : allows ref struct
    {
        LuauState state = source.Validate();
        lua_State* L = state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        using var _ = source.PushToTop();
        p1.Push(state);
        return InvokeAfterPush<TR>(state, L, nargs: 1);
    }

    internal static TR Invoke2<T, TR>(scoped in T? source, in IntoLuau p1, in IntoLuau p2)
        where T : IReferenceSource, allows ref struct
        where TR : allows ref struct
    {
        LuauState state = source.Validate();
        lua_State* L = state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        using var _ = source.PushToTop();
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
        if (luaReturn.TryGet(out TR? result, acceptNil: true))
            return result;

        throw new InvalidCastException(
            $"Could not convert Lua return value of type '{luaReturn.Type}' to '{typeof(TR).FullName}'."
        );
    }
}
