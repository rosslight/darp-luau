using System.Text;
using Luau.Native;
using static Luau.Native.NativeMethods;

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

    public ReadOnlySpan<byte> CheckStringOrNil(int parameterIndex) => throw new NotImplementedException();

    public double CheckNumber(int parameterIndex)
    {
        _state.ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(parameterIndex, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(parameterIndex, NumberOfParameters);
        lua_State* L = _state.L;
        var parameterType = (lua_Type)lua_type(L, parameterIndex);
        if (parameterType is not lua_Type.LUA_TNUMBER)
        {
            throw new ArgumentException(
                $"Parameter {parameterIndex} must be {lua_Type.LUA_TNUMBER} but was {parameterType}."
            );
        }
        return lua_tonumber(L, parameterIndex);
    }

    public double? CheckNumberOrNil(int parameterIndex) => throw new NotImplementedException();

    public LuauValue CheckLuauValue(int parameterIndex) => throw new NotImplementedException();

    public LuauTable CheckLuauTable(int parameterIndex) => throw new NotImplementedException();

    public LuauFunction CheckLuauFunction(int parameterIndex) => throw new NotImplementedException();

    public LuauString CheckLuauString(int parameterIndex) => throw new NotImplementedException();

    public void ReturnParameter(IntoLuau value)
    {
        _state.ThrowIfDisposed();
        value.Push(_state.L);
        NumberOfOutputParameters++;
    }
}
