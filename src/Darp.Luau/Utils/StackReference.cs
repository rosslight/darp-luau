using Darp.Luau.Native;

namespace Darp.Luau.Utils;

internal readonly ref struct StackReference(LuauState state, int stackIndex) : IReferenceSource
{
    private readonly LuauState _state = state;
    private readonly int _stackIndex = stackIndex;

    public LuauState ValidateInternal()
    {
        _state.ThrowIfDisposed();
        return _state;
    }

    public PopDisposable PushToStack(out int stackIndex)
    {
        stackIndex = _stackIndex;
        return default;
    }

    public unsafe PopDisposable PushToTop()
    {
        _state.ThrowIfDisposed();
        LuauNative.lua_pushvalue(_state.L, _stackIndex);
        return new PopDisposable(_state.L, true);
    }

    public override string ToString() => Helpers.StackString(_state, _stackIndex);
}
