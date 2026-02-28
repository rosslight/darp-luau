using System.Diagnostics.CodeAnalysis;
using Darp.Luau.Native;
using Darp.Luau.Utils;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau.Internal;

internal static unsafe class LuauUserdataAccessCore
{
    internal static bool TryGetManaged<T>(
        scoped in LuauRefSource source,
        string ownerTypeName,
        [NotNullWhen(true)] out T? value,
        [NotNullWhen(false)] out string? error
    )
        where T : class, ILuauUserData<T>
    {
        value = null;
        LuauState state = source.Validate(ownerTypeName);
        lua_State* L = state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        source.Push(L, ownerTypeName);
        bool ok = ManagedUserdataResolver.TryResolve(L, -1, out value, out error, valueLabel: ownerTypeName);
        lua_pop(L, 1);
        return ok;
    }
}
