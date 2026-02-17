using System.Text;
using Darp.Luau.Native;
using Darp.Luau.Utils;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau;

/// <summary> A lua exception </summary>
public sealed class LuaException : Exception
{
    private LuaException(string message)
        : base(message) { }

    /// <summary> Throws if not ok </summary>
    /// <param name="L"> The state that holds the error </param>
    /// <param name="status"> The status </param>
    /// <exception cref="LuaException"> The exception if the status is not ok </exception>
    public static unsafe void ThrowIfNotOk(lua_State* L, int status)
    {
        if (status == 0)
            return;

#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: -1);
#endif
        nuint outLength = 0;
        byte* err = lua_tolstring(L, -1, &outLength);
        lua_pop(L, 1);
        string error = err is null ? "<unknown lua error>" : Encoding.UTF8.GetString(err, (int)outLength);
        string message = $"Lua invocation failed with status {status}: {error}";
        throw new LuaException(message);
    }
}
