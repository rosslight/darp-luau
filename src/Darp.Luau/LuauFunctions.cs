using Luau.Native;
using static Luau.Native.NativeMethods;

namespace Darp.Luau;

/// <summary> A view on the lua state which provides access to methods relevant for operating inside a function callback </summary>
public unsafe ref struct LuauFunctions
{
    private readonly lua_State* L;

    /// <summary> The number of parameters this function was called with </summary>
    public int NumberOfParameters { get; }

    /// <summary> The number of output parameters registered </summary>
    public int NumberOfOutputParameters { get; private set; }

    [Obsolete("Do not initialize LuauFunctionX with the default constructor!", true)]
    public LuauFunctions() { }

    internal LuauFunctions(lua_State* l, int numberOfParameters)
    {
        L = l;
        NumberOfParameters = numberOfParameters;
    }

    /// <summary> Check if the parameter is a string and return it </summary>
    /// <param name="parameterIndex"> The index of the parameter. 1 based </param>
    /// <returns> The string bytes </returns>
    /// <remarks> The resulting span points to lua owned memory! If a GC cycle is triggered this span might no longer be valid! </remarks>
    public ReadOnlySpan<byte> CheckString(int parameterIndex)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(parameterIndex, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(parameterIndex, NumberOfParameters);
        var parameterType = (lua_Type)lua_type(L, 1);
        if (parameterType is not lua_Type.LUA_TSTRING)
        {
            throw new ArgumentOutOfRangeException(
                $"{parameterIndex}",
                $"Parameter is not of type {lua_Type.LUA_TSTRING} but is {parameterType}"
            );
        }
        nuint len;
        byte* pStr = lua_tolstring(L, 1, &len);
        if (pStr is null)
            throw new ArgumentOutOfRangeException($"{parameterIndex}", "Parameter returned null string");
        return new ReadOnlySpan<byte>(pStr, (int)len);
    }

    public ReadOnlySpan<byte> CheckStringOrNil(int parameterIndex) => throw new NotImplementedException();

    public double CheckNumber(int parameterIndex) => throw new NotImplementedException();

    public double? CheckNumberOrNil(int parameterIndex) => throw new NotImplementedException();

    public LuauValue CheckLuauValue(int parameterIndex) => throw new NotImplementedException();

    public LuauTable CheckLuauTable(int parameterIndex) => throw new NotImplementedException();

    public LuauFunction CheckLuauFunction(int parameterIndex) => throw new NotImplementedException();

    public LuauString CheckLuauString(int parameterIndex) => throw new NotImplementedException();

    public void ReturnParameter(IntoLuau returnParameter) => throw new NotImplementedException();
}
