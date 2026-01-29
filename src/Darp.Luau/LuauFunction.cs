using Luau.Native;
using static Luau.Native.NativeMethods;

namespace Darp.Luau;

public readonly ref struct LuauFunction : ILuauReference
{
    /// <inheritdoc />
    public LuauState? State { get; }

    /// <inheritdoc />
    public int Reference { get; }

    /// <summary> Do (not) initialize a new LuauFunction </summary>
    [Obsolete("Do not initialize the LuauFunction. Create using the LuauState instead", false)]
    public LuauFunction() => State = null;

    internal LuauFunction(LuauState? state, int reference) => (State, Reference) = (state, reference);

    public unsafe TR Call<TR>()
        where TR : allows ref struct
    {
        State.ThrowIfDisposed();
        lua_State* L = State.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        lua_getref(L, Reference);

        int nresults = typeof(TR) == typeof(LuauNil) ? 0 : 1;
        int status = lua_pcall(L, nargs: 0, nresults, 0);
        LuaException.ThrowIfNotOk(L, status);

        var luaReturn = LuauValue.ToValue(State);
        lua_pop(L, 1);
        return luaReturn.TryGet(out TR? result, acceptNil: true) ? result : throw new Exception();
    }

    public unsafe TR Call<TR>(IntoLuau p1)
        where TR : allows ref struct
    {
        State.ThrowIfDisposed();
        lua_State* L = State.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        lua_getref(L, Reference);

        p1.Push(L);

        int nresults = typeof(TR) == typeof(LuauNil) ? 0 : 1;
        int status = lua_pcall(L, nargs: 1, nresults, 0);
        LuaException.ThrowIfNotOk(L, status);

        var luaReturn = LuauValue.ToValue(State);
        lua_pop(L, 1);
        return luaReturn.TryGet(out TR? result, acceptNil: true) ? result : throw new Exception();
    }

    public unsafe TR Call<TR>(IntoLuau p1, IntoLuau p2)
        where TR : allows ref struct
    {
        State.ThrowIfDisposed();
        lua_State* L = State.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif

        lua_getref(L, Reference);
        p1.Push(L);
        p2.Push(L);

        int nresults = typeof(TR) == typeof(LuauNil) ? 0 : 1;
        int status = lua_pcall(L, nargs: 2, nresults, 0);
        LuaException.ThrowIfNotOk(L, status);

        var luaReturn = LuauValue.ToValue(State);
        lua_pop(L, 1);
        return luaReturn.TryGet(out TR? result, acceptNil: true) ? result : throw new Exception();
    }

    public static implicit operator IntoLuau(LuauFunction value) => (LuauValue)value;

    /// <inheritdoc />
    public override string ToString() => State is null ? "<nil>" : Helpers.RefToString(State, Reference);
}
