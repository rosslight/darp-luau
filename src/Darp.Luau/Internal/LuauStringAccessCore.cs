using System.Diagnostics.CodeAnalysis;
using System.Text;
using Darp.Luau.Native;
using Darp.Luau.Utils;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau.Internal;

internal static unsafe class LuauStringAccessCore
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
        nuint length = 0;
        byte* pStr = lua_tolstring(L, stackIndex, &length);
        if (pStr is null)
            return false;

        span = new ReadOnlySpan<byte>(pStr, checked((int)length));
        return true;
    }

    public static bool TryGet<T>(scoped in T source, [NotNullWhen(true)] out string? value)
        where T : IReferenceSource, allows ref struct
    {
        value = null;
        if (!TryGet(source, out ReadOnlySpan<byte> rawValue))
            return false;

        value = Encoding.UTF8.GetString(rawValue);
        return true;
    }
}
