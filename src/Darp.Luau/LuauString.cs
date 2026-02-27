using System.Text;
using Darp.Luau.Native;
using Darp.Luau.Utils;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau;

/// <summary> A reference to a Luau string value stored in the registry. </summary>
public readonly ref struct LuauString : ILuauReference
{
    /// <inheritdoc />
    public LuauState? State { get; }

    /// <inheritdoc />
    public int Reference { get; }

    /// <summary> Do not initialize directly. Create via <see cref="LuauState"/> APIs. </summary>
    [Obsolete("Do not initialize the LuauString. Create using the LuauState instead", true)]
    public LuauString() => State = null;

    internal LuauString(LuauState? state, int reference) => (State, Reference) = (state, reference);

    /// <summary> Releases this string reference from the state registry. </summary>
    public void Dispose() => State?.ReferenceTracker.ReleaseRef(Reference);

    /// <summary> Converts this string reference into an <see cref="IntoLuau"/> value. </summary>
    public static implicit operator IntoLuau(LuauString value) => (LuauValue)value;

    /// <inheritdoc />
    public override unsafe string ToString()
    {
        if (State?.ReferenceTracker.TryResolveLuaRef(Reference, out int reference) is not true)
            return "<nil>";

        lua_State* L = State.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        _ = lua_getref(L, reference);
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
