using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using Darp.Luau.Native;
using Darp.Luau.Utils;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau;

/// <summary> Input view used by <see cref="LuauState.CreateFunctionBuilder(LuauState.LuauFunctionBuilder)"/> callbacks </summary>
public readonly unsafe ref struct LuauArgs
{
    private readonly LuauState? _state;
    private readonly int _firstParameterStackIndex;

    /// <summary> The number of arguments passed from Lua </summary>
    public int ArgumentCount { get; }

    internal LuauArgs(LuauState state, int argumentCount)
        : this(state, argumentCount, firstParameterStackIndex: 1) { }

    internal LuauArgs(LuauState state, int argumentCount, int firstParameterStackIndex)
    {
        _state = state;
        ArgumentCount = argumentCount;
        _firstParameterStackIndex = firstParameterStackIndex;
    }

    /// <summary>
    /// Attempts to validate whether the number of arguments provided in Lua matches the expected number of arguments.
    /// </summary>
    /// <param name="expectedArgumentCount">The expected number of arguments.</param>
    /// <param name="error">An output parameter for storing an error message if the validation fails.</param>
    /// <returns>
    /// Returns <c>true</c> if the provided argument count matches the expected number; otherwise, <c>false</c>.
    /// </returns>
    public bool TryValidateArgumentCount(int expectedArgumentCount, [NotNullWhen(false)] out string? error)
    {
        _state.ThrowIfDisposed();
        if (ArgumentCount >= expectedArgumentCount)
        {
            error = null;
            return true;
        }
        error = $"Expected {expectedArgumentCount} arguments but got {ArgumentCount}.";
        return false;
    }

    private bool TryGetParameterContext(
        int parameterIndex,
        out lua_State* L,
        out int stackIndex,
        out lua_Type actualType,
        [NotNullWhen(false)] out string? error
    )
    {
        L = null;
        stackIndex = 0;
        actualType = default;
        error = null;

        // Throw exceptions for exceptional state. This should not be able to happen
        ArgumentOutOfRangeException.ThrowIfLessThan(parameterIndex, 0);
        _state.ThrowIfDisposed();

        if (parameterIndex > ArgumentCount)
        {
            error = $"Parameter index {parameterIndex} is out of range. Expected 1..{ArgumentCount}.";
            return false;
        }

        stackIndex = _firstParameterStackIndex + parameterIndex - 1;
        L = _state.L;
        actualType = (lua_Type)lua_type(L, stackIndex);
        return true;
    }

    private static bool TryRequireType(
        int parameterIndex,
        lua_Type actualType,
        lua_Type expectedType,
        [NotNullWhen(false)] out string? error
    )
    {
        if (actualType == expectedType)
        {
            error = null;
            return true;
        }

        error =
            actualType == lua_Type.LUA_TNIL
                ? $"Parameter {parameterIndex} is nil but {expectedType} is required."
                : $"Parameter {parameterIndex} must be {expectedType} but was {actualType}.";
        return false;
    }

    private static bool TryRequireTypeOrNil(
        int parameterIndex,
        lua_Type actualType,
        lua_Type expectedType,
        out bool isNil,
        [NotNullWhen(false)] out string? error
    )
    {
        if (actualType == lua_Type.LUA_TNIL)
        {
            isNil = true;
            error = null;
            return true;
        }

        isNil = false;
        if (actualType == expectedType)
        {
            error = null;
            return true;
        }

        error = $"Parameter {parameterIndex} must be {expectedType} or {lua_Type.LUA_TNIL} but was {actualType}.";
        return false;
    }

    /// <summary>
    /// Attempts to read a numeric parameter from the Lua stack at the given index.
    /// </summary>
    /// <param name="parameterIndex">The index of the parameter to read.</param>
    /// <param name="value">An output parameter that stores the numeric value if the read operation is successful.</param>
    /// <param name="error">An output parameter that contains an error message if the read operation fails.</param>
    /// <returns>
    /// Returns <c>true</c> if the parameter at the specified index is a valid number; otherwise, <c>false</c>.
    /// </returns>
    public bool TryReadNumber(int parameterIndex, out double value, [NotNullWhen(false)] out string? error)
    {
        value = 0;
        if (!TryGetParameterContext(parameterIndex, out lua_State* L, out int stackIndex, out lua_Type type, out error))
            return false;
        if (!TryRequireType(parameterIndex, type, lua_Type.LUA_TNUMBER, out error))
            return false;

        value = lua_tonumber(L, stackIndex);
        return true;
    }

    public bool TryReadNumberOrNil(int parameterIndex, out double? value, [NotNullWhen(false)] out string? error)
    {
        value = null;
        if (!TryGetParameterContext(parameterIndex, out lua_State* L, out int stackIndex, out lua_Type type, out error))
            return false;
        if (!TryRequireTypeOrNil(parameterIndex, type, lua_Type.LUA_TNUMBER, out bool isNil, out error))
            return false;
        if (isNil)
            return true;

        value = lua_tonumber(L, stackIndex);
        return true;
    }

    /// <remarks>
    /// The resulting span points to Lua owned memory.
    /// If a GC cycle is triggered this span might no longer be valid.
    /// </remarks>
    public bool TryReadUtf8String(
        int parameterIndex,
        out ReadOnlySpan<byte> value,
        [NotNullWhen(false)] out string? error
    )
    {
        value = default;
        if (!TryGetParameterContext(parameterIndex, out lua_State* L, out int stackIndex, out lua_Type type, out error))
            return false;
        if (!TryRequireType(parameterIndex, type, lua_Type.LUA_TSTRING, out error))
            return false;

        nuint len = 0;
        byte* pStr = lua_tolstring(L, stackIndex, &len);
        if (pStr is null)
        {
            error = $"Parameter {parameterIndex} returned a null string pointer.";
            return false;
        }

        value = new ReadOnlySpan<byte>(pStr, checked((int)len));
        return true;
    }

    /// <remarks>
    /// The resulting span points to Lua owned memory.
    /// If a GC cycle is triggered this span might no longer be valid.
    /// </remarks>
    public bool TryReadUtf8StringOrNil(
        int parameterIndex,
        out ReadOnlySpan<byte> value,
        out bool isNil,
        [NotNullWhen(false)] out string? error
    )
    {
        value = default;
        isNil = false;
        if (!TryGetParameterContext(parameterIndex, out lua_State* L, out int stackIndex, out lua_Type type, out error))
            return false;
        if (!TryRequireTypeOrNil(parameterIndex, type, lua_Type.LUA_TSTRING, out isNil, out error))
            return false;
        if (isNil)
            return true;

        nuint len = 0;
        byte* pStr = lua_tolstring(L, stackIndex, &len);
        if (pStr is null)
        {
            error = $"Parameter {parameterIndex} returned a null string pointer.";
            return false;
        }

        value = new ReadOnlySpan<byte>(pStr, checked((int)len));
        return true;
    }

    public bool TryReadBoolean(int parameterIndex, out bool value, [NotNullWhen(false)] out string? error)
    {
        value = false;
        if (!TryGetParameterContext(parameterIndex, out lua_State* L, out int stackIndex, out lua_Type type, out error))
            return false;
        if (!TryRequireType(parameterIndex, type, lua_Type.LUA_TBOOLEAN, out error))
            return false;

        value = lua_toboolean(L, stackIndex) == 1;
        return true;
    }

    public bool TryReadBooleanOrNil(int parameterIndex, out bool? value, [NotNullWhen(false)] out string? error)
    {
        value = null;
        if (!TryGetParameterContext(parameterIndex, out lua_State* L, out int stackIndex, out lua_Type type, out error))
            return false;
        if (!TryRequireTypeOrNil(parameterIndex, type, lua_Type.LUA_TBOOLEAN, out bool isNil, out error))
            return false;
        if (isNil)
        {
            value = null;
            return true;
        }

        value = lua_toboolean(L, stackIndex) == 1;
        return true;
    }

    /// <remarks>
    /// The resulting span points to Lua owned memory.
    /// If a GC cycle is triggered this span might no longer be valid.
    /// </remarks>
    public bool TryReadBuffer(int parameterIndex, out ReadOnlySpan<byte> value, [NotNullWhen(false)] out string? error)
    {
        value = default;
        if (!TryGetParameterContext(parameterIndex, out lua_State* L, out int stackIndex, out lua_Type type, out error))
            return false;
        if (!TryRequireType(parameterIndex, type, lua_Type.LUA_TBUFFER, out error))
            return false;

        nuint nLength = 0;
        void* pBuf = lua_tobuffer(L, stackIndex, &nLength);
        if (pBuf is null)
        {
            error = $"Parameter {parameterIndex} returned a null pointer.";
            return false;
        }

        value = new ReadOnlySpan<byte>(pBuf, checked((int)nLength));
        return true;
    }

    /// <remarks>
    /// The resulting span points to Lua owned memory.
    /// If a GC cycle is triggered this span might no longer be valid.
    /// </remarks>
    public bool TryReadBufferOrNil(
        int parameterIndex,
        out ReadOnlySpan<byte> value,
        out bool isNil,
        [NotNullWhen(false)] out string? error
    )
    {
        value = default;
        isNil = false;
        if (!TryGetParameterContext(parameterIndex, out lua_State* L, out int stackIndex, out lua_Type type, out error))
            return false;
        if (!TryRequireTypeOrNil(parameterIndex, type, lua_Type.LUA_TBUFFER, out isNil, out error))
            return false;
        if (isNil)
            return true;

        nuint nLength = 0;
        void* pBuf = lua_tobuffer(L, stackIndex, &nLength);
        if (pBuf is null)
        {
            error = $"Parameter {parameterIndex} returned a null pointer.";
            return false;
        }

        value = new ReadOnlySpan<byte>(pBuf, checked((int)nLength));
        return true;
    }

    public bool TryReadLuauValue(int parameterIndex, out LuauValue value, [NotNullWhen(false)] out string? error)
    {
        value = default;
        _state.ThrowIfDisposed();
        if (!TryGetParameterContext(parameterIndex, out lua_State* L, out int stackIndex, out _, out error))
            return false;

        value = LuauValue.ToValue(_state, stackIndex);
        return true;
    }

    public bool TryReadLuauTable(int parameterIndex, out LuauTable value, [NotNullWhen(false)] out string? error)
    {
        value = default;
        if (!TryGetParameterContext(parameterIndex, out lua_State* L, out int stackIndex, out lua_Type type, out error))
            return false;
        if (!TryRequireType(parameterIndex, type, lua_Type.LUA_TTABLE, out error))
            return false;

        int reference = lua_ref(L, stackIndex);
        value = new LuauTable(_state, reference);
        return true;
    }

    public bool TryReadLuauFunction(int parameterIndex, out LuauFunction value, [NotNullWhen(false)] out string? error)
    {
        value = default;
        if (!TryGetParameterContext(parameterIndex, out lua_State* L, out int stackIndex, out lua_Type type, out error))
            return false;
        if (!TryRequireType(parameterIndex, type, lua_Type.LUA_TFUNCTION, out error))
            return false;

        int reference = lua_ref(L, stackIndex);
        value = new LuauFunction(_state, reference);
        return true;
    }

    public bool TryReadLuauString(int parameterIndex, out LuauString value, [NotNullWhen(false)] out string? error)
    {
        value = default;
        if (!TryGetParameterContext(parameterIndex, out lua_State* L, out int stackIndex, out lua_Type type, out error))
            return false;
        if (!TryRequireType(parameterIndex, type, lua_Type.LUA_TSTRING, out error))
            return false;

        int reference = lua_ref(L, stackIndex);
        value = new LuauString(_state, reference);
        return true;
    }

    public bool TryReadLuauBuffer(int parameterIndex, out LuauBuffer value, [NotNullWhen(false)] out string? error)
    {
        value = default;
        if (!TryGetParameterContext(parameterIndex, out lua_State* L, out int stackIndex, out lua_Type type, out error))
            return false;
        if (!TryRequireType(parameterIndex, type, lua_Type.LUA_TBUFFER, out error))
            return false;

        int reference = lua_ref(L, stackIndex);
        value = new LuauBuffer(_state!, reference);
        return true;
    }

    public bool TryRead<T>(int parameterIndex, [NotNullWhen(true)] out T? value, [NotNullWhen(false)] out string? error)
        where T : allows ref struct
    {
        value = default!;
        error = null;
        if (!TryValidateArgumentCount(parameterIndex, out error))
            return false;

        if (typeof(T) == typeof(double))
        {
            if (!TryReadNumber(parameterIndex, out double result, out error))
                return false;
            value = Unsafe.As<double, T>(ref result)!;
            return true;
        }
        if (typeof(T) == typeof(int))
        {
            if (!TryReadNumber(parameterIndex, out double result, out error))
                return false;
            int temp = (int)result;
            value = Unsafe.As<int, T>(ref temp)!;
            return true;
        }

        if (typeof(T) == typeof(bool))
        {
            if (!TryReadBoolean(parameterIndex, out bool result, out error))
                return false;
            value = Unsafe.As<bool, T>(ref result)!;
            return true;
        }

        if (typeof(T) == typeof(string))
        {
            if (!TryReadUtf8String(parameterIndex, out ReadOnlySpan<byte> result, out error))
                return false;
            string temp = Encoding.UTF8.GetString(result);
            value = Unsafe.As<string, T>(ref temp)!;
            return true;
        }

        if (typeof(T) == typeof(LuauValue))
        {
            if (!TryReadLuauValue(parameterIndex, out LuauValue result, out error))
                return false;
            value = Unsafe.As<LuauValue, T>(ref result)!;
            return true;
        }

        if (typeof(T) == typeof(LuauTable))
        {
            if (!TryReadLuauTable(parameterIndex, out LuauTable result, out error))
                return false;
            value = Unsafe.As<LuauTable, T>(ref result)!;
            return true;
        }

        if (typeof(T) == typeof(LuauFunction))
        {
            if (!TryReadLuauFunction(parameterIndex, out LuauFunction result, out error))
                return false;
            value = Unsafe.As<LuauFunction, T>(ref result)!;
            return true;
        }

        if (typeof(T) == typeof(LuauString))
        {
            if (!TryReadLuauString(parameterIndex, out LuauString result, out error))
                return false;
            value = Unsafe.As<LuauString, T>(ref result)!;
            return true;
        }

        if (typeof(T) == typeof(LuauBuffer))
        {
            if (!TryReadLuauBuffer(parameterIndex, out LuauBuffer result, out error))
                return false;
            value = Unsafe.As<LuauBuffer, T>(ref result)!;
            return true;
        }

        error = $"Cannot read lua type {typeof(T)}. It is not supported.";
        value = default;
        return false;
    }
}
