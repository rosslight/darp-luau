using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Luau.Native;
using static Luau.Native.NativeMethods;

namespace Darp.Luau;

public enum LuauValueType
{
    // Nil has to be 0 to allow default(LuauValue) to not cause problems
    Nil = 0,
    String,
    Number,
    Boolean,
    Table,
    Function,
    Thread,
    UserData,
    Vector,
    Buffer,
}

[StructLayout(LayoutKind.Sequential)]
public readonly ref struct LuauValue
{
    public LuauValueType Type { get; }
    private readonly LuauState? _state;
    private readonly LuauValueUnion _union;

    [StructLayout(LayoutKind.Explicit)]
    private readonly ref struct LuauValueUnion
    {
        [FieldOffset(0)]
        public readonly bool ValueBool;

        [FieldOffset(0)]
        public readonly double ValueDouble;

        [FieldOffset(0)]
        public readonly int ValueReference;

        public LuauValueUnion(bool value) => ValueBool = value;

        public LuauValueUnion(double value) => ValueDouble = value;

        public LuauValueUnion(int value) => ValueReference = value;
    }

    private LuauValue(LuauState? state, LuauValueType type, LuauValueUnion union)
    {
        Type = type;
        _state = state;
        _union = union;
    }

    public static implicit operator LuauValue(bool value) =>
        new(null, LuauValueType.Boolean, new LuauValueUnion(value));

    public static implicit operator LuauValue(double value) =>
        new(null, LuauValueType.Number, new LuauValueUnion(value));

    public static implicit operator LuauValue(LuauString value) =>
        new(value.State, LuauValueType.String, new LuauValueUnion(value.Reference));

    public static implicit operator LuauValue(LuauTable value) =>
        new(value.State, LuauValueType.Table, new LuauValueUnion(value.Reference));

    public static implicit operator LuauValue(LuauFunction value) =>
        new(value.State, LuauValueType.Function, new LuauValueUnion(value.Reference));

    public bool TryGet(out bool value)
    {
        if (Type is not LuauValueType.Boolean)
        {
            value = false;
            return false;
        }
        value = _union.ValueBool;
        return true;
    }

    public bool TryGet(out double value)
    {
        if (Type is not LuauValueType.Number)
        {
            value = 0;
            return false;
        }
        value = _union.ValueDouble;
        return true;
    }

    public bool TryGet(out LuauString value)
    {
        if (Type is not LuauValueType.String || _state is null)
        {
            value = default;
            return false;
        }
        value = new LuauString(_state, _union.ValueReference);
        return true;
    }

    public bool TryGet(out LuauTable value)
    {
        if (Type is not LuauValueType.Table || _state is null)
        {
            value = default;
            return false;
        }
        value = new LuauTable(_state, _union.ValueReference);
        return true;
    }

    public bool TryGet(out LuauFunction value)
    {
        if (Type is not LuauValueType.Function || _state is null)
        {
            value = default;
            return false;
        }
        value = new LuauFunction(_state, _union.ValueReference);
        return true;
    }

    /// <summary> Push a single value on the stack </summary>
    /// <param name="L"> The lua state </param>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown if the Type is invalid </exception>
    internal unsafe void Push(lua_State* L)
    {
        Debug.Assert(_state is null || _state.L == L);
        switch (Type)
        {
            case LuauValueType.Nil:
                lua_pushnil(L);
                break;
            case LuauValueType.Boolean:
                lua_pushboolean(L, _union.ValueBool ? 1 : 0);
                break;
            case LuauValueType.Number:
                lua_pushnumber(L, _union.ValueDouble);
                break;
            case LuauValueType.String or LuauValueType.Table or LuauValueType.Function:
                lua_getref(L, _union.ValueReference);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    internal static unsafe bool TryPop(LuauState state, out LuauValue value)
    {
        value = ToValue(state, -1);
        lua_pop(state.L, 1);
        return true;
    }

    private static unsafe LuauValue ToValue(LuauState state, int index)
    {
        lua_State* L = state.L;
        var type = (lua_Type)lua_type(L, index);
        switch (type)
        {
            case lua_Type.LUA_TNIL:
                return default;
            case lua_Type.LUA_TBOOLEAN:
                bool valueBool = lua_toboolean(L, index) == 1;
                return new LuauValue(state, LuauValueType.Boolean, new LuauValueUnion(valueBool));
            case lua_Type.LUA_TNUMBER:
                double valueNumber = lua_tonumber(L, index);
                return new LuauValue(state, LuauValueType.Number, new LuauValueUnion(valueNumber));
            case lua_Type.LUA_TSTRING:
                int referenceString = lua_ref(L, index);
                return new LuauValue(state, LuauValueType.String, new LuauValueUnion(referenceString));
            case lua_Type.LUA_TTABLE:
                int referenceTable = lua_ref(L, index);
                return new LuauValue(state, LuauValueType.Table, new LuauValueUnion(referenceTable));
            case lua_Type.LUA_TFUNCTION:
                int referenceFunction = lua_ref(L, index);
                return new LuauValue(state, LuauValueType.Table, new LuauValueUnion(referenceFunction));
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public static bool TryCreate<T>(T value, LuauState state, out LuauValue luauValue)
        where T : allows ref struct
    {
        ArgumentNullException.ThrowIfNull(state);
        if (value is null)
        {
            luauValue = default;
            return true;
        }
        if (typeof(T) == typeof(ReadOnlySpan<byte>))
        {
            luauValue = state.CreateString(Unsafe.As<T, ReadOnlySpan<byte>>(ref value));
            return true;
        }
        if (typeof(T) == typeof(ReadOnlySpan<char>))
        {
            luauValue = state.CreateString(Unsafe.As<T, ReadOnlySpan<char>>(ref value));
            return true;
        }
        if (typeof(T) == typeof(string))
        {
            luauValue = state.CreateString(Unsafe.As<T, string>(ref value));
            return true;
        }
        if (typeof(T) == typeof(double))
        {
            luauValue = new LuauValue(state, LuauValueType.Number, new LuauValueUnion(Unsafe.As<T, double>(ref value)));
            return true;
        }
        luauValue = default;
        return false;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Type switch
        {
            LuauValueType.Nil => "nil",
            LuauValueType.String when _state is null => "string",
            LuauValueType.String => new LuauString(_state, _union.ValueReference).ToString(),
            LuauValueType.Number => _union.ValueDouble.ToString(CultureInfo.InvariantCulture),
            LuauValueType.Boolean => _union.ValueBool ? "true" : "false",
            _ => "n/a",
        };
    }
}
