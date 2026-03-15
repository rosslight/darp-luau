using Darp.Luau.Native;
using Darp.Luau.Utils;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau.Internal;

internal static unsafe class LuauBufferAccessCore
{
    internal static bool TryGet<T>(scoped in T source, out ReadOnlySpan<byte> span)
        where T : IReferenceSource, allows ref struct
    {
        span = ReadOnlySpan<byte>.Empty;
        LuauState state = source.Validate();
        lua_State* L = state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        using PopDisposable _ = source.PushToStack(out int stackIndex);
        nuint nLength = 0;
        void* pBuf = lua_tobuffer(L, stackIndex, &nLength);
        if (pBuf is null)
            return false;

        span = new ReadOnlySpan<byte>(pBuf, checked((int)nLength));
        return true;
    }

    public static bool TryGet<T>(scoped in T source, out byte[] bytes)
        where T : IReferenceSource, allows ref struct
    {
        if (TryGet(source, out ReadOnlySpan<byte> span))
        {
            bytes = span.ToArray();
            return true;
        }

        bytes = [];
        return false;
    }
}
