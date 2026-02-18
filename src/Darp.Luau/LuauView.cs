using Darp.Luau.Native;
using Darp.Luau.Utils;
using static Darp.Luau.Native.LuauNative;
using static Darp.Luau.Utils.LuauNativeMethods;

namespace Darp.Luau;

public readonly unsafe ref struct LuauView
{
    private readonly LuauState? _state;
    private readonly int _stackIndex;

    internal LuauView(LuauState state, int stackIndex)
    {
        _state = state;
        _stackIndex = stackIndex;
    }

    public readonly ReadOnlySpan<byte> CheckString()
    {
        _state.ThrowIfDisposed();
        lua_State* L = _state.L;
        var parameterType = (lua_Type)lua_type(L, _stackIndex);
        if (parameterType is not lua_Type.LUA_TSTRING)
            throw new ArgumentException($"Value must be {lua_Type.LUA_TSTRING} but was {parameterType}.");

        nuint len;
        byte* pStr = lua_tolstring(L, _stackIndex, &len);
        if (pStr is null)
            throw new ArgumentException("Value returned a null string pointer.");
        return new ReadOnlySpan<byte>(pStr, (int)len);
    }

    public readonly double CheckNumber()
    {
        _state.ThrowIfDisposed();
        lua_State* L = _state.L;
        var parameterType = (lua_Type)lua_type(L, _stackIndex);
        if (parameterType is not lua_Type.LUA_TNUMBER)
            throw new ArgumentException($"Value must be {lua_Type.LUA_TNUMBER} but was {parameterType}.");

        return lua_tonumber(L, _stackIndex);
    }

    public readonly bool CheckBoolean()
    {
        _state.ThrowIfDisposed();
        lua_State* L = _state.L;
        var parameterType = (lua_Type)lua_type(L, _stackIndex);
        if (parameterType is not lua_Type.LUA_TBOOLEAN)
            throw new ArgumentException($"Value must be {lua_Type.LUA_TBOOLEAN} but was {parameterType}.");

        return lua_toboolean(L, _stackIndex) == 1;
    }

    public readonly LuauTable CheckTable()
    {
        _state.ThrowIfDisposed();
        lua_State* L = _state.L;
        var parameterType = (lua_Type)lua_type(L, _stackIndex);
        if (parameterType is not lua_Type.LUA_TTABLE)
            throw new ArgumentException($"Value must be {lua_Type.LUA_TTABLE} but was {parameterType}.");

        lua_pushvalue(L, _stackIndex);
        int reference = luaL_ref(L, LUA_REGISTRYINDEX);
        return new LuauTable(_state, reference);
    }

    public readonly LuauFunction CheckFunction()
    {
        _state.ThrowIfDisposed();
        lua_State* L = _state.L;
        var parameterType = (lua_Type)lua_type(L, _stackIndex);
        if (parameterType is not lua_Type.LUA_TFUNCTION)
            throw new ArgumentException($"Value must be {lua_Type.LUA_TFUNCTION} but was {parameterType}.");

        lua_pushvalue(L, _stackIndex);
        int reference = luaL_ref(L, LUA_REGISTRYINDEX);
        return new LuauFunction(_state, reference);
    }

    public readonly LuauValue CheckValue()
    {
        _state.ThrowIfDisposed();
        lua_State* L = _state.L;

        lua_pushvalue(L, _stackIndex);
        try
        {
            return LuauValue.ToValue(_state);
        }
        finally
        {
            lua_pop(L, 1);
        }
    }

    public readonly LuauValue CheckLuauValue() => CheckValue();
}
