using System.Runtime.CompilerServices;
using Darp.Luau.Native;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau;

public struct LuauBuffer : ILuauReference, IDisposable
{
    /// <inheritdoc />
    public LuauState? State { get; }

    /// <inheritdoc />
    public int Reference { get; private set; }

    [Obsolete("Do not initialize the LuauBuffer. Create using the LuauState instead", true)]
    public LuauBuffer() => State = null;

    internal LuauBuffer(LuauState? state, int reference) => (State, Reference) = (state, reference);

    public static implicit operator IntoLuau(LuauBuffer value) => (LuauValue)value;

    public unsafe bool TryGet(out ReadOnlySpan<byte> span)
    {
        State.ThrowIfDisposed();
        span = ReadOnlySpan<byte>.Empty;

        lua_State* L = State.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        _ = lua_getref(L, Reference);
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
    public unsafe void Dispose()
    {
        if (State is null || Reference is 0)
            return;

        lua_unref(State.L, Reference);
        Reference = 0;
    }
}
