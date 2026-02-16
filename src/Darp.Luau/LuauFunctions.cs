using Darp.Luau.Native;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau;

/// <summary> A view on the lua state which provides access to methods relevant for operating inside a function callback </summary>
public unsafe ref struct LuauFunctions
{
    private readonly LuauState? _state;

    /// <summary> The number of parameters this function was called with </summary>
    public int NumberOfParameters { get; }

    /// <summary> The number of output parameters registered </summary>
    public int NumberOfOutputParameters { get; private set; }

    [Obsolete("Do not initialize LuauFunctionX with the default constructor!", true)]
    public LuauFunctions() { }

    internal LuauFunctions(LuauState state, int numberOfParameters)
    {
        _state = state;
        NumberOfParameters = numberOfParameters;
    }

    /// <summary> Check if the parameter is a string and return it </summary>
    /// <param name="parameterIndex"> The index of the parameter. 1 based </param>
    /// <returns> The string bytes </returns>
    /// <remarks> The resulting span points to lua owned memory! If a GC cycle is triggered this span might no longer be valid! </remarks>
    public ReadOnlySpan<byte> CheckString(int parameterIndex)
    {
        _state.ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(parameterIndex, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(parameterIndex, NumberOfParameters);
        lua_State* L = _state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        var parameterType = (lua_Type)lua_type(L, parameterIndex);
        if (parameterType is not lua_Type.LUA_TSTRING)
        {
            throw new ArgumentException(
                $"Parameter {parameterIndex} must be {lua_Type.LUA_TSTRING} but was {parameterType}."
            );
        }
        nuint len;
        byte* pStr = lua_tolstring(L, parameterIndex, &len);
        if (pStr is null)
            throw new ArgumentException($"Parameter {parameterIndex} returned a null string pointer.");
        return new ReadOnlySpan<byte>(pStr, (int)len);
    }

    public ReadOnlySpan<byte> CheckStringOrNil(int parameterIndex, out bool isNull)
    {
        _state.ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(parameterIndex, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(parameterIndex, NumberOfParameters);
        lua_State* L = _state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        var parameterType = (lua_Type)lua_type(L, parameterIndex);
        if (parameterType is lua_Type.LUA_TNIL)
        {
            isNull = true;
            return null;
        }
        if (parameterType is not lua_Type.LUA_TSTRING)
        {
            throw new ArgumentException(
                $"Parameter {parameterIndex} must be {lua_Type.LUA_TSTRING} but was {parameterType}."
            );
        }
        nuint len;
        byte* pStr = lua_tolstring(L, parameterIndex, &len);
        if (pStr is null)
            throw new ArgumentException($"Parameter {parameterIndex} returned a null string pointer.");
        isNull = false;
        return new ReadOnlySpan<byte>(pStr, (int)len);
    }

    public double CheckNumber(int parameterIndex)
    {
        _state.ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(parameterIndex, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(parameterIndex, NumberOfParameters);
        lua_State* L = _state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        var parameterType = (lua_Type)lua_type(L, parameterIndex);
        if (parameterType is not lua_Type.LUA_TNUMBER)
        {
            throw new ArgumentException(
                $"Parameter {parameterIndex} must be {lua_Type.LUA_TNUMBER} but was {parameterType}."
            );
        }
        return lua_tonumber(L, parameterIndex);
    }

    public bool CheckBoolean(int parameterIndex)
    {
        _state.ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(parameterIndex, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(parameterIndex, NumberOfParameters);
        lua_State* L = _state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        var parameterType = (lua_Type)lua_type(L, parameterIndex);
        if (parameterType is not lua_Type.LUA_TBOOLEAN)
        {
            throw new ArgumentException(
                $"Parameter {parameterIndex} must be {lua_Type.LUA_TBOOLEAN} but was {parameterType}."
            );
        }
        return lua_toboolean(L, parameterIndex) == 1;
    }

    public bool? CheckBooleanOrNil(int parameterIndex)
    {
        _state.ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(parameterIndex, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(parameterIndex, NumberOfParameters);
        lua_State* L = _state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        var parameterType = (lua_Type)lua_type(L, parameterIndex);
        if (parameterType is lua_Type.LUA_TNIL)
        {
            return null;
        }
        if (parameterType is not lua_Type.LUA_TBOOLEAN)
        {
            throw new ArgumentException(
                $"Parameter {parameterIndex} must be {lua_Type.LUA_TBOOLEAN} or {lua_Type.LUA_TNIL} but was {parameterType}."
            );
        }
        return lua_toboolean(L, parameterIndex) == 1;
    }

    public double? CheckNumberOrNil(int parameterIndex)
    {
        _state.ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(parameterIndex, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(parameterIndex, NumberOfParameters);
        lua_State* L = _state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        var parameterType = (lua_Type)lua_type(L, parameterIndex);
        if (parameterType is lua_Type.LUA_TNIL)
        {
            return null;
        }
        if (parameterType is not lua_Type.LUA_TNUMBER)
        {
            throw new ArgumentException(
                $"Parameter {parameterIndex} must be {lua_Type.LUA_TNUMBER} or {lua_Type.LUA_TNIL} but was {parameterType}."
            );
        }
        return lua_tonumber(L, parameterIndex);
    }

    /// <summary> Check if the parameter is a buffer and return it </summary>
    /// <param name="parameterIndex"> The index of the parameter. 1 based </param>
    /// <returns> The buffer bytes </returns>
    /// <remarks> The resulting span points to lua owned memory! If a GC cycle is triggered this span might no longer be valid! </remarks>
    public ReadOnlySpan<byte> CheckBuffer(int parameterIndex)
    {
        _state.ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(parameterIndex, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(parameterIndex, NumberOfParameters);
        lua_State* L = _state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        var parameterType = (lua_Type)lua_type(L, parameterIndex);
        if (parameterType is not lua_Type.LUA_TBUFFER)
        {
            throw new ArgumentException(
                $"Parameter {parameterIndex} must be {lua_Type.LUA_TBUFFER} but was {parameterType}."
            );
        }
        
        nuint nLength = 0;
        void* pBuf = lua_tobuffer(L, -1, &nLength);
        if (pBuf is null)
            throw new ArgumentException($"Parameter {parameterIndex} returned a null pointer.");
            
        return new ReadOnlySpan<byte>(pBuf, (int)nLength);
    }

    public ReadOnlySpan<byte> CheckBufferOrNil(int parameterIndex, out bool isNull)
    {
        _state.ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(parameterIndex, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(parameterIndex, NumberOfParameters);
        lua_State* L = _state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 0);
#endif
        var parameterType = (lua_Type)lua_type(L, parameterIndex);
        if (parameterType is lua_Type.LUA_TNIL)
        {
            isNull = true;
            return null;
        }
        if (parameterType is not lua_Type.LUA_TBUFFER)
        {
            throw new ArgumentException(
                $"Parameter {parameterIndex} must be {lua_Type.LUA_TBUFFER} but was {parameterType}."
            );
        }

        nuint nLength = 0;
        void* pBuf = lua_tobuffer(L, -1, &nLength);
        if (pBuf is null)
            throw new ArgumentException($"Parameter {parameterIndex} returned a null pointer.");

        isNull = false;
        return new ReadOnlySpan<byte>(pBuf, (int)nLength);
    }

    public LuauValue CheckLuauValue(int parameterIndex) => throw new NotImplementedException();

    public LuauTable CheckLuauTable(int parameterIndex) => throw new NotImplementedException();

    public LuauFunction CheckLuauFunction(int parameterIndex) => throw new NotImplementedException();

    public LuauString CheckLuauString(int parameterIndex) => throw new NotImplementedException();

    public void ReturnParameter(IntoLuau value)
    {
        _state.ThrowIfDisposed();
        lua_State* L = _state.L;
#if DEBUG
        using var guard = new StackGuard(L, expectedDelta: 1);
#endif
        value.Push(L);
        NumberOfOutputParameters++;
    }
}
