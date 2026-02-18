using System.Diagnostics.CodeAnalysis;
using Darp.Luau.Native;
using Darp.Luau.Utils;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau;

public struct LuauFunction : ILuauReference
{
    /// <inheritdoc />
    public LuauState? State { get; }

    /// <inheritdoc />
    public int Reference { get; private set; }

    /// <summary> Do (not) initialize a new LuauFunction </summary>
    [Obsolete("Do not initialize the LuauFunction. Create using the LuauState instead", false)]
    public LuauFunction() => State = null;

    internal LuauFunction(LuauState? state, int reference) => (State, Reference) = (state, reference);

    public readonly unsafe TR Call<TR>()
        where TR : allows ref struct
    {
        ThrowIfDisposed();
        lua_State* L = State.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        lua_getref(L, Reference);

        int nresults = typeof(TR) == typeof(LuauNil) ? 0 : 1;
        int status = lua_pcall(L, nargs: 0, nresults, 0);
        LuaException.ThrowIfNotOk(L, status, "lua_pcall");

        if (nresults == 0)
            return default!;

        var luaReturn = LuauValue.ToValue(State);
        lua_pop(L, 1);
        if (luaReturn.TryGet(out TR? result, acceptNil: true))
            return result;

        throw new InvalidCastException(
            $"Could not convert Lua return value of type '{luaReturn.Type}' to '{typeof(TR).FullName}'."
        );
    }

    public readonly unsafe TR Call<TR>(IntoLuau p1)
        where TR : allows ref struct
    {
        ThrowIfDisposed();
        lua_State* L = State.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        lua_getref(L, Reference);

        p1.Push(State);

        int nresults = typeof(TR) == typeof(LuauNil) ? 0 : 1;
        int status = lua_pcall(L, nargs: 1, nresults, 0);
        LuaException.ThrowIfNotOk(L, status, "lua_pcall");

        if (nresults == 0)
            return default!;

        var luaReturn = LuauValue.ToValue(State);
        lua_pop(L, 1);
        if (luaReturn.TryGet(out TR? result, acceptNil: true))
            return result;

        throw new InvalidCastException(
            $"Could not convert Lua return value of type '{luaReturn.Type}' to '{typeof(TR).FullName}'."
        );
    }

    public readonly unsafe TR Call<TR>(IntoLuau p1, IntoLuau p2)
        where TR : allows ref struct
    {
        ThrowIfDisposed();
        lua_State* L = State.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif

        lua_getref(L, Reference);
        p1.Push(State);
        p2.Push(State);

        int nresults = typeof(TR) == typeof(LuauNil) ? 0 : 1;
        int status = lua_pcall(L, nargs: 2, nresults, 0);
        LuaException.ThrowIfNotOk(L, status, "lua_pcall");

        if (nresults == 0)
            return default!;

        var luaReturn = LuauValue.ToValue(State);
        lua_pop(L, 1);
        if (luaReturn.TryGet(out TR? result, acceptNil: true))
            return result;

        throw new InvalidCastException(
            $"Could not convert Lua return value of type '{luaReturn.Type}' to '{typeof(TR).FullName}'."
        );
    }

    public static implicit operator IntoLuau(LuauFunction value) => (LuauValue)value;

    /// <inheritdoc />
    public override readonly string ToString() => State is null ? "<nil>" : Helpers.RefToString(State, Reference);

    /// <summary> Remove the reference from the lua state </summary>
    public unsafe void Dispose()
    {
        if (State is null || Reference is 0)
            return;
        lua_unref(State.L, Reference);
        Reference = 0;
    }

    [MemberNotNull(nameof(State))]
    private readonly void ThrowIfDisposed()
    {
        State.ThrowIfDisposed();
        if (Reference is 0)
            throw new ObjectDisposedException(nameof(LuauTable), "The reference to the LuauTable is invalid");
    }
}
