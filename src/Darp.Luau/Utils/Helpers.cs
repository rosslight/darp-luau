using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Darp.Luau.Internal;
using Darp.Luau.Native;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau.Utils;

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
    /// <param name="handle"> The tracked handle </param>
    /// <returns> The resulting string </returns>
    public static unsafe string HandleToString(LuauState? state, ulong handle)
    {
        if (state is null || state.IsDisposed)
            return "<nil>";
        lua_State* L = state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        var trackedReference = state.GetTrackedReferenceOrThrow(handle);
        using PopDisposable _ = trackedReference.PushToTop(); // [value]
        fixed (byte* pToStrFunc = "tostring"u8)
        {
            var type = (lua_Type)lua_getglobal(L, pToStrFunc); // [value, tostring]
            Debug.Assert(type == lua_Type.LUA_TFUNCTION);
        }
        lua_pushvalue(L, -2); // [value, tostring, value]
        lua_call(L, 1, 1); // [value, result]

        nuint length;
        byte* pStr = lua_tolstring(L, -1, &length);
        string str = pStr is null ? "<no_str>" : Encoding.UTF8.GetString(pStr, (int)length);
        lua_pop(L, 1);
        return str;
    }

    /// <summary> Return the string representation of a string </summary>
    /// <param name="state"> The state the stackIndex is associated with </param>
    /// <param name="stackIndex"> The stackIndex </param>
    /// <returns> The resulting string </returns>
    public static unsafe string StackString(LuauState state, int stackIndex)
    {
        state.ThrowIfDisposed();
        lua_State* L = state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        var toStringFunc = "tostring"u8;

        fixed (byte* pToStrFunc = toStringFunc)
        {
            lua_getglobal(L, pToStrFunc); // [tostring]
        }
        lua_pushvalue(L, stackIndex < 0 ? stackIndex - 1 : stackIndex); // [tostring, value]
        lua_call(L, 1, 1); // [value, result]

        nuint length;
        byte* pStr = lua_tolstring(L, -1, &length);
        string str = pStr is null ? "<no_str>" : Encoding.UTF8.GetString(pStr, (int)length);
        lua_pop(L, 1);
        return str;
    }
}
