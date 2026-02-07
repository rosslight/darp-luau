using System.Runtime.CompilerServices;
using Luau.Native;
using static Luau.Native.NativeMethods;

namespace Darp.Luau;

public readonly ref struct LuauBuffer
{
    internal LuauState? State { get; }
    internal int Reference { get; }

    [Obsolete("Do not initialize the LuauBuffer. Create using the LuauState instead", true)]
    public LuauBuffer() => State = null;

    internal LuauBuffer(LuauState? state, int reference) => (State, Reference) = (state, reference);
    
    public static implicit operator IntoLuau(LuauBuffer value) => (LuauValue)value;  

    public unsafe bool TryGet(out ReadOnlySpan<byte> span)
    {
        span = ReadOnlySpan<byte>.Empty;

        if (State is null)
            return false;

        lua_State* L = State.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        try
        {
            _ = lua_getref(L, Reference);

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

    public unsafe bool TryGet(out byte[] bytes)
    {
        bytes = [];

        if (State is null)
            return false;

        lua_State* L = State.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        try
        {
            _ = lua_getref(L, Reference);

            nuint nLength = 0;
            void* pSrc = lua_tobuffer(L, -1, &nLength);
            if (pSrc is null)
                return false;

            bytes = new byte[(int)nLength];
            fixed(void* pDest = bytes)
            {
                Unsafe.CopyBlock(pDest, pSrc, (uint)nLength);
            }
            return true;
        }
        finally
        {
            lua_pop(L, 1);
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (!TryGet(out ReadOnlySpan<byte> span))
            return "<nil>";

        return Convert.ToHexString(span);
    }
}