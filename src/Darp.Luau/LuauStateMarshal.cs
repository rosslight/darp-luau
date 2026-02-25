using System.Runtime.CompilerServices;
using System.Text;
using Darp.Luau.Native;
using Darp.Luau.Utils;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau;

/// <summary> Unsafe operations to access the <see cref="lua_State"/> </summary>
internal static class LuauStateMarshal
{
    public static unsafe int ReturnResult(lua_State* state, LuaResult<int, string> result)
    {
        return result.TryGetValue(out int value, out string? error)
            ? ReturnSuccess(state, value)
            : ReturnError(state, error);
    }

    public static unsafe int ReturnError(lua_State* state, ReadOnlySpan<byte> message)
    {
        if (message.IsEmpty)
            message = "something went wrong"u8;
        lua_pushboolean(state, 0);
        PushString(state, message);
        return 2;
    }

    public static unsafe int ReturnError(lua_State* state, ReadOnlySpan<char> message)
    {
        if (message.IsEmpty)
            message = "something went wrong";
        lua_pushboolean(state, 0);
        PushString(state, message);
        return 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe int ReturnCallbackException(lua_State* state, string callbackName, Exception exception)
    {
        string callbackError = $"{callbackName} callback failed: {exception.GetType().Name}: {exception.Message}";
        return ReturnError(state, callbackError);
    }

    /// <summary>
    /// Prefixes an existing list of output values on the stack with the success flag.
    /// </summary>
    /// <remarks>
    /// Expects the top <paramref name="outputCount"/> values to be output values.
    /// </remarks>
    public static unsafe int ReturnSuccess(lua_State* state, int outputCount)
    {
        if (outputCount <= 0)
        {
            lua_pushboolean(state, 1);
            return 1;
        }

        int top = lua_gettop(state);
        if (outputCount > top)
            throw new ArgumentOutOfRangeException(
                nameof(outputCount),
                outputCount,
                "Output count exceeds current stack."
            );

        int firstOutputIndex = top - outputCount + 1;
        Span<int> outputReferences = outputCount <= 16 ? stackalloc int[outputCount] : new int[outputCount];
        for (int i = 0; i < outputCount; i++)
        {
            lua_pushvalue(state, firstOutputIndex + i);
            int reference = lua_ref(state, -1);
            lua_pop(state, 1);
            outputReferences[i] = reference;
        }

        lua_settop(state, top - outputCount);
        lua_pushboolean(state, 1);
        for (int i = 0; i < outputCount; i++)
        {
            int reference = outputReferences[i];
            lua_getref(state, reference);
            lua_unref(state, reference);
        }

        return 1 + outputCount;
    }

    public static unsafe bool TryGetString(lua_State* L, int stackIndex, out ReadOnlySpan<byte> value)
    {
        if ((lua_Type)lua_type(L, stackIndex) is not lua_Type.LUA_TSTRING)
        {
            value = default;
            return false;
        }

        nuint keyLength;
        byte* keyUtf8 = lua_tolstring(L, stackIndex, &keyLength);
        if (keyUtf8 is null)
        {
            value = default;
            return false;
        }
        value = new ReadOnlySpan<byte>(keyUtf8, checked((int)keyLength));
        return true;
    }

    public static unsafe bool TryGetNameCall(lua_State* L, out ReadOnlySpan<byte> methodName)
    {
        int atom = 0;
        byte* name = lua_namecallatom(L, &atom);
        if (name is null)
        {
            methodName = default;
            return false;
        }

        int length = 0;
        while (name[length] != 0)
            length++;

        methodName = new ReadOnlySpan<byte>(name, length);
        return true;
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
