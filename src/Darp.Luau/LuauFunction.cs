using System.Diagnostics.CodeAnalysis;
using Darp.Luau.Native;
using Darp.Luau.Utils;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau;

public readonly struct LuauFunction : ILuauReference
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
        ThrowIfDisposed();
        lua_State* L = State.L;
        int reference = State.ReferenceTracker.ResolveLuaRef(Reference, nameof(LuauFunction));
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        lua_getref(L, reference);

        int nresults = typeof(TR) == typeof(LuauNil) ? 0 : 1;
        int status = lua_pcall(L, nargs: 0, nresults, 0);
        LuaException.ThrowIfNotOk(L, status, "lua_pcall");

        if (nresults == 0)
            return default!;

        using var luaReturn = LuauValue.ToValue(State);
        lua_pop(L, 1);
        if (luaReturn.TryGet(out TR? result, acceptNil: true))
            return result;

        throw new InvalidCastException(
            $"Could not convert Lua return value of type '{luaReturn.Type}' to '{typeof(TR).FullName}'."
        );
    }

    public unsafe TR Call<TR>(IntoLuau p1)
        where TR : allows ref struct
    {
        ThrowIfDisposed();
        lua_State* L = State.L;
        int reference = State.ReferenceTracker.ResolveLuaRef(Reference, nameof(LuauFunction));
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        lua_getref(L, reference);

        p1.Push(State);

        int nresults = typeof(TR) == typeof(LuauNil) ? 0 : 1;
        int status = lua_pcall(L, nargs: 1, nresults, 0);
        LuaException.ThrowIfNotOk(L, status, "lua_pcall");

        if (nresults == 0)
            return default!;

        using var luaReturn = LuauValue.ToValue(State);
        lua_pop(L, 1);
        if (luaReturn.TryGet(out TR? result, acceptNil: true))
            return result;

        throw new InvalidCastException(
            $"Could not convert Lua return value of type '{luaReturn.Type}' to '{typeof(TR).FullName}'."
        );
    }

    public unsafe TR Call<TR>(IntoLuau p1, IntoLuau p2)
        where TR : allows ref struct
    {
        ThrowIfDisposed();
        lua_State* L = State.L;
        int reference = State.ReferenceTracker.ResolveLuaRef(Reference, nameof(LuauFunction));
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif

        lua_getref(L, reference);
        p1.Push(State);
        p2.Push(State);

        int nresults = typeof(TR) == typeof(LuauNil) ? 0 : 1;
        int status = lua_pcall(L, nargs: 2, nresults, 0);
        LuaException.ThrowIfNotOk(L, status, "lua_pcall");

        if (nresults == 0)
            return default!;

        using var luaReturn = LuauValue.ToValue(State);
        lua_pop(L, 1);
        if (luaReturn.TryGet(out TR? result, acceptNil: true))
            return result;

        throw new InvalidCastException(
            $"Could not convert Lua return value of type '{luaReturn.Type}' to '{typeof(TR).FullName}'."
        );
    }

    public static implicit operator IntoLuau(LuauFunction value) => (LuauValue)value;

    /// <inheritdoc />
    public override string ToString()
    {
        if (State?.ReferenceTracker.TryResolveLuaRef(Reference, out int reference) is not true)
            return "<nil>";
        return Helpers.RefToString(State, reference);
    }

    /// <summary> Remove the reference from the lua state </summary>
    public void Dispose() => State?.ReferenceTracker.ReleaseRef(Reference);

    [MemberNotNull(nameof(State))]
    private void ThrowIfDisposed()
    {
        State.ThrowIfDisposed();
        if (Reference is 0 || !State.ReferenceTracker.HasRegistryReference(Reference))
            throw new ObjectDisposedException(nameof(LuauFunction), "The reference to the LuauFunction is invalid");
    }
}
