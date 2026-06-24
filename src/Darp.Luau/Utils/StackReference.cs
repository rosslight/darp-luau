using Darp.Luau.Native;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau.Utils;

internal readonly unsafe ref struct StackReference : IReferenceSource
{
    private readonly LuauState _state;
    private readonly lua_State* _L;
    private readonly int _stackIndex;

    public StackReference(LuauState state, lua_State* L, int stackIndex)
    {
        ArgumentNullException.ThrowIfNull(L);
        if (!state.OwnsThread(L))
            throw new InvalidOperationException("Cross-state stack reference usage is not allowed.");

        _state = state;
        _L = L;
        _stackIndex = stackIndex;
    }

    public lua_State* L => _L;

    public LuauState ValidateInternal()
    {
        _state.ThrowIfDisposed();
        return _state;
    }

    public PopDisposable PushToStack(out int stackIndex)
    {
        _state.ThrowIfDisposed();
        lua_pushvalue(_L, _stackIndex);
        if ((nint)_L != (nint)_state.L)
            lua_xmove(_L, _state.L, 1);
        stackIndex = lua_gettop(_state.L);
        return new PopDisposable(_state.L, true);
    }

    public PopDisposable PushToTop()
    {
        _state.ThrowIfDisposed();
        lua_pushvalue(_L, _stackIndex);
        if ((nint)_L != (nint)_state.L)
            lua_xmove(_L, _state.L, 1);
        return new PopDisposable(_state.L, true);
    }

    public override string ToString()
    {
        using PopDisposable _ = PushToStack(out int stackIndex);
        return Helpers.StackString(_state, stackIndex);
    }
}
