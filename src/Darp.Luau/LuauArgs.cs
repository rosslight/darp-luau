using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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
        if (expectedArgumentCount == ArgumentCount)
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

    private int GetStackIndex(int parameterIndex)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(parameterIndex, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(parameterIndex, ArgumentCount);
        return _firstParameterStackIndex + parameterIndex - 1;
    }

    public ReadOnlySpan<byte> CheckString(int parameterIndex)
    {
        _state.ThrowIfDisposed();
        int stackIndex = GetStackIndex(parameterIndex);
        lua_State* L = _state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        var parameterType = (lua_Type)lua_type(L, stackIndex);
        if (parameterType is not lua_Type.LUA_TSTRING)
        {
            throw new ArgumentException(
                $"Parameter {parameterIndex} must be {lua_Type.LUA_TSTRING} but was {parameterType}."
            );
        }

        nuint len;
        byte* pStr = lua_tolstring(L, stackIndex, &len);
        if (pStr is null)
            throw new ArgumentException($"Parameter {parameterIndex} returned a null string pointer.");
        return new ReadOnlySpan<byte>(pStr, (int)len);
    }

    public ReadOnlySpan<byte> CheckStringOrNil(int parameterIndex, out bool isNull)
    {
        _state.ThrowIfDisposed();
        int stackIndex = GetStackIndex(parameterIndex);
        lua_State* L = _state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        var parameterType = (lua_Type)lua_type(L, stackIndex);
        if (parameterType is lua_Type.LUA_TNIL)
        {
            isNull = true;
            return default;
        }

        if (parameterType is not lua_Type.LUA_TSTRING)
        {
            throw new ArgumentException(
                $"Parameter {parameterIndex} must be {lua_Type.LUA_TSTRING} but was {parameterType}."
            );
        }

        nuint len;
        byte* pStr = lua_tolstring(L, stackIndex, &len);
        if (pStr is null)
            throw new ArgumentException($"Parameter {parameterIndex} returned a null string pointer.");

        isNull = false;
        return new ReadOnlySpan<byte>(pStr, (int)len);
    }

    public double CheckNumber(int parameterIndex)
    {
        _state.ThrowIfDisposed();
        int stackIndex = GetStackIndex(parameterIndex);
        lua_State* L = _state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        var parameterType = (lua_Type)lua_type(L, stackIndex);
        if (parameterType is not lua_Type.LUA_TNUMBER)
        {
            throw new ArgumentException(
                $"Parameter {parameterIndex} must be {lua_Type.LUA_TNUMBER} but was {parameterType}."
            );
        }

        return lua_tonumber(L, stackIndex);
    }

    public double? CheckNumberOrNil(int parameterIndex)
    {
        _state.ThrowIfDisposed();
        int stackIndex = GetStackIndex(parameterIndex);
        lua_State* L = _state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        var parameterType = (lua_Type)lua_type(L, stackIndex);
        if (parameterType is lua_Type.LUA_TNIL)
            return null;

        if (parameterType is not lua_Type.LUA_TNUMBER)
        {
            throw new ArgumentException(
                $"Parameter {parameterIndex} must be {lua_Type.LUA_TNUMBER} or {lua_Type.LUA_TNIL} but was {parameterType}."
            );
        }

        return lua_tonumber(L, stackIndex);
    }

    public bool CheckBoolean(int parameterIndex)
    {
        _state.ThrowIfDisposed();
        int stackIndex = GetStackIndex(parameterIndex);
        lua_State* L = _state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        var parameterType = (lua_Type)lua_type(L, stackIndex);
        if (parameterType is not lua_Type.LUA_TBOOLEAN)
        {
            throw new ArgumentException(
                $"Parameter {parameterIndex} must be {lua_Type.LUA_TBOOLEAN} but was {parameterType}."
            );
        }

        return lua_toboolean(L, stackIndex) == 1;
    }

    public bool? CheckBooleanOrNil(int parameterIndex)
    {
        _state.ThrowIfDisposed();
        int stackIndex = GetStackIndex(parameterIndex);
        lua_State* L = _state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        var parameterType = (lua_Type)lua_type(L, stackIndex);
        if (parameterType is lua_Type.LUA_TNIL)
            return null;

        if (parameterType is not lua_Type.LUA_TBOOLEAN)
        {
            throw new ArgumentException(
                $"Parameter {parameterIndex} must be {lua_Type.LUA_TBOOLEAN} or {lua_Type.LUA_TNIL} but was {parameterType}."
            );
        }

        return lua_toboolean(L, stackIndex) == 1;
    }

    public ReadOnlySpan<byte> CheckBuffer(int parameterIndex)
    {
        _state.ThrowIfDisposed();
        int stackIndex = GetStackIndex(parameterIndex);
        lua_State* L = _state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        var parameterType = (lua_Type)lua_type(L, stackIndex);
        if (parameterType is not lua_Type.LUA_TBUFFER)
        {
            throw new ArgumentException(
                $"Parameter {parameterIndex} must be {lua_Type.LUA_TBUFFER} but was {parameterType}."
            );
        }

        nuint nLength = 0;
        void* pBuf = lua_tobuffer(L, stackIndex, &nLength);
        if (pBuf is null)
            throw new ArgumentException($"Parameter {parameterIndex} returned a null pointer.");

        return new ReadOnlySpan<byte>(pBuf, (int)nLength);
    }

    public ReadOnlySpan<byte> CheckBufferOrNil(int parameterIndex, out bool isNull)
    {
        _state.ThrowIfDisposed();
        int stackIndex = GetStackIndex(parameterIndex);
        lua_State* L = _state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        var parameterType = (lua_Type)lua_type(L, stackIndex);
        if (parameterType is lua_Type.LUA_TNIL)
        {
            isNull = true;
            return default;
        }

        if (parameterType is not lua_Type.LUA_TBUFFER)
        {
            throw new ArgumentException(
                $"Parameter {parameterIndex} must be {lua_Type.LUA_TBUFFER} but was {parameterType}."
            );
        }

        nuint nLength = 0;
        void* pBuf = lua_tobuffer(L, stackIndex, &nLength);
        if (pBuf is null)
            throw new ArgumentException($"Parameter {parameterIndex} returned a null pointer.");

        isNull = false;
        return new ReadOnlySpan<byte>(pBuf, (int)nLength);
    }

    public LuauValue CheckLuauValue(int parameterIndex)
    {
        _state.ThrowIfDisposed();
        int stackIndex = GetStackIndex(parameterIndex);
        lua_State* L = _state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        lua_pushvalue(L, stackIndex);
        try
        {
            return LuauValue.ToValue(_state);
        }
        finally
        {
            lua_pop(L, 1);
        }
    }

    public LuauTable CheckLuauTable(int parameterIndex)
    {
        _state.ThrowIfDisposed();
        int stackIndex = GetStackIndex(parameterIndex);
        lua_State* L = _state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        var parameterType = (lua_Type)lua_type(L, stackIndex);
        if (parameterType is not lua_Type.LUA_TTABLE)
        {
            throw new ArgumentException(
                $"Parameter {parameterIndex} must be {lua_Type.LUA_TTABLE} but was {parameterType}."
            );
        }

        lua_pushvalue(L, stackIndex);
        int reference = LuauNativeMethods.luaL_ref(L, LUA_REGISTRYINDEX);
        return new LuauTable(_state, reference);
    }

    public LuauFunction CheckLuauFunction(int parameterIndex)
    {
        _state.ThrowIfDisposed();
        int stackIndex = GetStackIndex(parameterIndex);
        lua_State* L = _state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        var parameterType = (lua_Type)lua_type(L, stackIndex);
        if (parameterType is not lua_Type.LUA_TFUNCTION)
        {
            throw new ArgumentException(
                $"Parameter {parameterIndex} must be {lua_Type.LUA_TFUNCTION} but was {parameterType}."
            );
        }

        lua_pushvalue(L, stackIndex);
        int reference = LuauNativeMethods.luaL_ref(L, LUA_REGISTRYINDEX);
        return new LuauFunction(_state, reference);
    }

    public LuauString CheckLuauString(int parameterIndex)
    {
        _state.ThrowIfDisposed();
        int stackIndex = GetStackIndex(parameterIndex);
        lua_State* L = _state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        var parameterType = (lua_Type)lua_type(L, stackIndex);
        if (parameterType is not lua_Type.LUA_TSTRING)
        {
            throw new ArgumentException(
                $"Parameter {parameterIndex} must be {lua_Type.LUA_TSTRING} but was {parameterType}."
            );
        }

        lua_pushvalue(L, stackIndex);
        int reference = LuauNativeMethods.luaL_ref(L, LUA_REGISTRYINDEX);
        return new LuauString(_state, reference);
    }

    public LuauBuffer CheckLuauBuffer(int parameterIndex)
    {
        _state.ThrowIfDisposed();
        int stackIndex = GetStackIndex(parameterIndex);
        lua_State* L = _state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        var parameterType = (lua_Type)lua_type(L, stackIndex);
        if (parameterType is not lua_Type.LUA_TBUFFER)
        {
            throw new ArgumentException(
                $"Parameter {parameterIndex} must be {lua_Type.LUA_TBUFFER} but was {parameterType}."
            );
        }

        lua_pushvalue(L, stackIndex);
        int reference = LuauNativeMethods.luaL_ref(L, LUA_REGISTRYINDEX);
        return new LuauBuffer(_state, reference);
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
        if (!TryGetParameterContext(parameterIndex, out _, out _, out _, out error))
            return false;

        value = CheckLuauValue(parameterIndex);
        return true;
    }

    public bool TryReadLuauTable(int parameterIndex, out LuauTable value, [NotNullWhen(false)] out string? error)
    {
        value = default;
        if (!TryGetParameterContext(parameterIndex, out _, out _, out lua_Type type, out error))
            return false;
        if (!TryRequireType(parameterIndex, type, lua_Type.LUA_TTABLE, out error))
            return false;

        value = CheckLuauTable(parameterIndex);
        return true;
    }

    public bool TryReadLuauFunction(int parameterIndex, out LuauFunction value, [NotNullWhen(false)] out string? error)
    {
        value = default;
        if (!TryGetParameterContext(parameterIndex, out _, out _, out lua_Type type, out error))
            return false;
        if (!TryRequireType(parameterIndex, type, lua_Type.LUA_TFUNCTION, out error))
            return false;

        value = CheckLuauFunction(parameterIndex);
        return true;
    }

    public bool TryReadLuauString(int parameterIndex, out LuauString value, [NotNullWhen(false)] out string? error)
    {
        value = default;
        if (!TryGetParameterContext(parameterIndex, out lua_State* L, out int stackIndex, out lua_Type type, out error))
            return false;
        if (!TryRequireType(parameterIndex, type, lua_Type.LUA_TSTRING, out error))
            return false;

#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        lua_pushvalue(L, stackIndex);
        int reference = LuauNativeMethods.luaL_ref(L, LUA_REGISTRYINDEX);
        value = new LuauString(_state!, reference);
        return true;
    }

    public bool TryReadLuauBuffer(int parameterIndex, out LuauBuffer value, [NotNullWhen(false)] out string? error)
    {
        value = default;
        if (!TryGetParameterContext(parameterIndex, out lua_State* L, out int stackIndex, out lua_Type type, out error))
            return false;
        if (!TryRequireType(parameterIndex, type, lua_Type.LUA_TBUFFER, out error))
            return false;

#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        lua_pushvalue(L, stackIndex);
        int reference = LuauNativeMethods.luaL_ref(L, LUA_REGISTRYINDEX);
        value = new LuauBuffer(_state!, reference);
        return true;
    }

    public bool TryRead(int parameterIndex, out int value, [NotNullWhen(false)] out string? error)
    {
        value = 0;
        if (!TryReadNumber(parameterIndex, out double numericValue, out error))
            return false;

        value = (int)numericValue;
        return true;
    }

    public bool TryRead<T>(int parameterIndex, out T value, [NotNullWhen(false)] out string? error)
        where T : allows ref struct
    {
        value = default!;
        error = null;

        if (typeof(T) == typeof(double))
        {
            if (!TryReadNumber(parameterIndex, out double result, out error))
                return false;

            value = Unsafe.As<double, T>(ref result)!;
            return true;
        }

        if (typeof(T) == typeof(bool))
        {
            if (!TryReadBoolean(parameterIndex, out bool result, out error))
                return false;

            value = Unsafe.As<bool, T>(ref result)!;
            return true;
        }

        if (typeof(T) == typeof(ReadOnlySpan<byte>))
        {
            if (!TryReadUtf8String(parameterIndex, out ReadOnlySpan<byte> result, out error))
                return false;

            value = Unsafe.As<ReadOnlySpan<byte>, T>(ref result)!;
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

        if (parameterIndex <= 0 || parameterIndex > ArgumentCount)
        {
            error = $"Parameter index {parameterIndex} is out of range. Expected 1..{ArgumentCount}.";
            return false;
        }

        try
        {
            LuauValue luauValue = CheckLuauValue(parameterIndex);
            if (luauValue.TryGet(out T? result, acceptNil: true))
            {
                value = result;
                return true;
            }

            error =
                $"Parameter {parameterIndex} could not be converted from Lua type '{luauValue.Type}' to '{typeof(T).FullName}'.";
            return false;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }
}
