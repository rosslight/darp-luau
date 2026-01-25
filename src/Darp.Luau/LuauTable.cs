using Luau.Native;
using static Luau.Native.NativeMethods;

namespace Darp.Luau;

public readonly ref struct LuauTable
{
    private readonly LuauState? _state;

    internal LuauState State
    {
        get
        {
            if (_state is null)
                throw new InvalidOperationException("LuauState is not initialized.");
            ObjectDisposedException.ThrowIf(_state.IsDisposed, _state);
            return _state;
        }
    }

    internal int Reference { get; }

    [Obsolete("Do not initialize the LuauTable. Create using the LuauState instead", true)]
    public LuauTable() { }

    internal LuauTable(LuauState state, int pointer)
    {
        _state = state;
        Reference = pointer;
    }

    public unsafe void Set(LuauValue key, LuauValue value)
    {
        lua_State* L = State.L;
        lua_getref(L, Reference);
        key.Push(L);
        value.Push(L);
        lua_settable(L, -3);
        lua_pop(L, 1);
    }

    public unsafe bool TryGet(LuauValue key, out LuauValue value)
    {
        LuauState state = State;
        lua_State* L = state.L;
        lua_getref(L, Reference);
        key.Push(L);
        _ = lua_gettable(L, -2);
        return LuauValue.TryPop(state, out value);
    }

    /// <inheritdoc />
    public override string ToString() => "TODO";
}
