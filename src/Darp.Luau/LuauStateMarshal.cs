using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Darp.Luau.Native;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau;

/// <summary> Unsafe operations to access the <see cref="lua_State"/> </summary>
internal static class LuauStateMarshal
{
    [DoesNotReturn]
    public static unsafe int RaiseLuaError(lua_State* state, ReadOnlySpan<byte> message)
    {
        if (message.IsEmpty)
            message = "something went wrong"u8;
        PushString(state, message);
        lua_error(state);
        throw new UnreachableException("lua_error should not return");
    }

    [DoesNotReturn]
    public static unsafe int RaiseLuaError(lua_State* state, ReadOnlySpan<char> message)
    {
        if (message.IsEmpty)
            message = "something went wrong";
        PushString(state, message);
        lua_error(state);
        throw new UnreachableException("lua_error should not return");
    }

    [DoesNotReturn]
    public static unsafe int RaiseCallbackException(lua_State* state, string callbackName, Exception exception)
    {
        string callbackError = $"{callbackName} callback failed: {exception.GetType().Name}: {exception.Message}";
        return RaiseLuaError(state, callbackError);
    }

    public static unsafe void PushString(lua_State* state, string? message)
    {
        if (message is null)
        {
            lua_pushnil(state);
            return;
        }
        PushString(state, message.AsSpan());
    }

    public static unsafe void PushString(lua_State* state, ReadOnlySpan<char> message)
    {
        Span<byte> utf8Message = stackalloc byte[Encoding.UTF8.GetMaxByteCount(message.Length)];
        int utf8Length = Encoding.UTF8.GetBytes(message, utf8Message);
        fixed (byte* pMessage = utf8Message)
        {
            lua_pushlstring(state, pMessage, (nuint)utf8Length);
        }
    }

    public static unsafe void PushString(lua_State* state, ReadOnlySpan<byte> message)
    {
        fixed (byte* pMessage = message)
        {
            lua_pushlstring(state, pMessage, (nuint)message.Length);
        }
    }
}
