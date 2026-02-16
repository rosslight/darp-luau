using System.Diagnostics.CodeAnalysis;
using System.Text;
using Darp.Luau.Native;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau;

internal static class Helpers
{
    /// <summary> Throw if the <see cref="LuauState"/> is not present or disposed </summary>
    /// <param name="state"> The state to check </param>
    /// <exception cref="InvalidOperationException"> Thrown if the state is null/uninitialized </exception>
    /// <exception cref="ObjectDisposedException"> Thrown if the state is disposed </exception>
    public static void ThrowIfDisposed([NotNull] this LuauState? state)
    {
        if (state is null)
            throw new InvalidOperationException("No LuauState present.");
        ObjectDisposedException.ThrowIf(state.IsDisposed, state);
    }

    /// <summary> Return the string representation of a string </summary>
    /// <param name="state"> The state the reference is associated with </param>
    /// <param name="reference"> The reference </param>
    /// <returns> The resulting string </returns>
    public static unsafe string RefToString(LuauState state, int reference)
    {
        lua_State* L = state.L;
        var toStringFunc = "tostring"u8;

        lua_getref(L, reference); // [value]
        fixed (byte* pToStrFunc = toStringFunc)
        {
            lua_getglobal(L, pToStrFunc); // [value, tostring]
        }
        lua_pushvalue(L, -2); // [value, tostring, value]
        lua_call(L, 1, 1); // [value, result]

        nuint length;
        byte* pStr = lua_tolstring(L, -1, &length);
        string str = pStr is null ? "<no_str>" : Encoding.UTF8.GetString(pStr, (int)length);
        lua_pop(L, 2);
        return str;
    }
}
