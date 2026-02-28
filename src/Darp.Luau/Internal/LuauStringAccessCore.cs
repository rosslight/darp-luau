using System.Diagnostics.CodeAnalysis;
using System.Text;
using Darp.Luau.Native;
using Darp.Luau.Utils;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau.Internal;

internal static unsafe class LuauStringAccessCore
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
            nuint length = 0;
            byte* pStr = lua_tolstring(L, -1, &length);
            if (pStr is null)
                return false;

            span = new ReadOnlySpan<byte>(pStr, checked((int)length));
            return true;
        }
        finally
        {
            lua_pop(L, 1);
        }
    }

    public static bool TryGet(
        scoped in LuauRefSource source,
        string ownerTypeName,
        [NotNullWhen(true)] out string? value
    )
    {
        value = null;
        if (!TryGet(source, ownerTypeName, out ReadOnlySpan<byte> rawValue))
            return false;

        value = Encoding.UTF8.GetString(rawValue);
        return true;
    }
}
