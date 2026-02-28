using Darp.Luau.Native;
using Darp.Luau.Utils;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau.Internal;

internal static unsafe class LuauBufferAccessCore
{
    internal static bool TryGet(scoped in LuauRefSource source, string ownerTypeName, out ReadOnlySpan<byte> span)
    {
        span = ReadOnlySpan<byte>.Empty;
        LuauState state = source.Validate(ownerTypeName);
        lua_State* L = state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        source.Push(L, ownerTypeName);
        try
        {
            nuint nLength = 0;
            void* pBuf = lua_tobuffer(L, -1, &nLength);
            if (pBuf is null)
                return false;

            span = new ReadOnlySpan<byte>(pBuf, checked((int)nLength));
            return true;
        }
        finally
        {
            lua_pop(L, 1);
        }
    }

    public static bool TryGet(scoped in LuauRefSource source, string ownerTypeName, out byte[] bytes)
    {
        if (TryGet(source, ownerTypeName, out ReadOnlySpan<byte> span))
        {
            bytes = span.ToArray();
            return true;
        }

        bytes = [];
        return false;
    }
}
