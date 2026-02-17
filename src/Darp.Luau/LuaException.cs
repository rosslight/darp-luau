using System.Text;
using Darp.Luau.Native;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau;

/// <summary> A lua exception </summary>
public sealed class LuaException : Exception
{
    private LuaException(string message)
        : base(message) { }

    /// <summary> Throws if not ok </summary>
    /// <param name="state"> The state that holds the error </param>
    /// <param name="status"> The status </param>
    /// <exception cref="LuaException"> The exception if the status is not ok </exception>
    public static unsafe void ThrowIfNotOk(lua_State* state, int status)
    {
        if (status == 0)
            return;

        nuint outLength = 0;
        byte* err = lua_tolstring(state, -1, &outLength);
        lua_pop(state, 1);
        string error = Encoding.UTF8.GetString(err, (int)outLength);
        string message = $"Lua invocation failed with status {status}: {error}";
        throw new LuaException(message);
    }
}
