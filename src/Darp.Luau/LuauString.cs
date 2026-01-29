using System.Text;
using Luau.Native;
using static Luau.Native.NativeMethods;

namespace Darp.Luau;

public readonly ref struct LuauString
{
    internal LuauState? State { get; }
    internal int Reference { get; }

    [Obsolete("Do not initialize the LuauString. Create using the LuauState instead", true)]
    public LuauString() => State = null;

    internal LuauString(LuauState? state, int reference) => (State, Reference) = (state, reference);

    public static implicit operator IntoLuau(LuauString value) => (LuauValue)value;

    /// <inheritdoc />
    public override unsafe string ToString()
    {
        if (State is null)
            return "<nil>";
        lua_State* L = State.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        _ = lua_getref(L, Reference);
        nuint length = 0;
        byte* pStr = lua_tolstring(L, -1, &length);
        if (pStr is null)
        {
            lua_pop(L, 1);
            return "<nil>";
        }
        string str = Encoding.UTF8.GetString(pStr, (int)length);
        lua_pop(L, 1);
        return str;
    }
}
