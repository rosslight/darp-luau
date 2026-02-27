using System.Diagnostics.CodeAnalysis;
using Darp.Luau.Native;
using Darp.Luau.Utils;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau;

public readonly ref struct LuauBuffer : ILuauReference
{
    /// <inheritdoc />
    public LuauState? State { get; }

    /// <inheritdoc />
    public int Reference { get; }

    [Obsolete("Do not initialize the LuauBuffer. Create using the LuauState instead", true)]
    public LuauBuffer() => State = null;

    internal LuauBuffer(LuauState? state, int reference) => (State, Reference) = (state, reference);

    /// <summary> Ability for <see cref="LuauBuffer"/> to be passed into functions that accept <see cref="IntoLuau"/> </summary>
    /// <param name="value"> The buffer </param>
    /// <returns> The converted value </returns>
    public static implicit operator IntoLuau(LuauBuffer value) => (LuauValue)value;

    public unsafe bool TryGet(out ReadOnlySpan<byte> span)
    {
        ThrowIfDisposed();
        span = ReadOnlySpan<byte>.Empty;

        lua_State* L = State.L;
        int reference = State.ReferenceTracker.ResolveLuaRef(Reference, nameof(LuauBuffer));
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        _ = lua_getref(L, reference);
        try
        {
            nuint nLength = 0;
            void* pBuf = lua_tobuffer(L, -1, &nLength);
            if (pBuf is null)
                return false;

            span = new ReadOnlySpan<byte>(pBuf, (int)nLength);
            return true;
        }
        finally
        {
            lua_pop(L, 1);
        }
    }

    public bool TryGet(out byte[] bytes)
    {
        if (TryGet(out ReadOnlySpan<byte> span))
        {
            bytes = span.ToArray();
            return true;
        }

        bytes = [];
        return false;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (!TryGet(out ReadOnlySpan<byte> span))
            return "<nil>";

        return Convert.ToHexString(span);
    }

    /// <summary> Remove the reference from the lua state </summary>
    public void Dispose() => State?.ReferenceTracker.ReleaseRef(Reference);

    [MemberNotNull(nameof(State))]
    private void ThrowIfDisposed()
    {
        State.ThrowIfDisposed();
        if (Reference is 0 || !State.ReferenceTracker.HasRegistryReference(Reference))
            throw new ObjectDisposedException(nameof(LuauBuffer), "The reference to the LuauBuffer is invalid");
    }
}
