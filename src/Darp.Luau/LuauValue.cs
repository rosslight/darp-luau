using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
public readonly struct LuauValue
{
    public LuauValueType Type { get; }
    private readonly LuauState? _state;
    private readonly LuauValueUnion _union;

    [StructLayout(LayoutKind.Explicit)]
    private readonly struct LuauValueUnion
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

    public static implicit operator LuauValue(LuauBuffer value) =>
        new(value.State, LuauValueType.Buffer, new LuauValueUnion(value.Reference));

    public bool TryGet<T>([NotNullWhen(true)] out T? value, bool acceptNil = false)
        where T : allows ref struct
    {
        value = default;
        switch (Type)
        {
            case LuauValueType.Nil:
                if (typeof(T) == typeof(LuauNil))
                {
                    var temp = default(LuauNil);
                    value = Unsafe.As<LuauNil, T>(ref temp)!;
                    return true;
                }
                if (!acceptNil)
                {
                    return false;
                }
                if (typeof(T) == typeof(double?))
                {
                    double? temp = null;
                    value = Unsafe.As<double?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(Half?))
                {
                    Half? temp = null;
                    value = Unsafe.As<Half?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(float?))
                {
                    float? temp = null;
                    value = Unsafe.As<float?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(decimal?))
                {
                    decimal? temp = null;
                    value = Unsafe.As<decimal?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(double?))
                {
                    double? temp = null;
                    value = Unsafe.As<double?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(sbyte?))
                {
                    sbyte? temp = null;
                    value = Unsafe.As<sbyte?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(byte?))
                {
                    byte? temp = null;
                    value = Unsafe.As<byte?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(short?))
                {
                    short? temp = null;
                    value = Unsafe.As<short?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(ushort?))
                {
                    ushort? temp = null;
                    value = Unsafe.As<ushort?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(int?))
                {
                    int? temp = null;
                    value = Unsafe.As<int?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(uint?))
                {
                    uint? temp = null;
                    value = Unsafe.As<uint?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(long?))
                {
                    long? temp = null;
                    value = Unsafe.As<long?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(ulong?))
                {
                    ulong? temp = null;
                    value = Unsafe.As<ulong?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(Int128?))
                {
                    Int128? temp = null;
                    value = Unsafe.As<Int128?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(UInt128?))
                {
                    UInt128? temp = null;
                    value = Unsafe.As<UInt128?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(bool?))
                {
                    bool? temp = null;
                    value = Unsafe.As<bool?, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(string))
                {
                    string? temp = null;
                    value = Unsafe.As<string?, T>(ref temp)!;
                    return true;
                }
                return false;
            case LuauValueType.String:
                if (typeof(T) == typeof(string))
                {
                    string temp = new LuauString(_state, _union.ValueReference).ToString();
                    value = Unsafe.As<string, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(LuauString))
                {
                    var temp = new LuauString(_state, _union.ValueReference);
                    value = Unsafe.As<LuauString, T>(ref temp)!;
                    return true;
                }
                return false;
            case LuauValueType.Number:
                if (typeof(T) == typeof(double))
                {
                    double temp = _union.ValueDouble;
                    value = Unsafe.As<double, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(Half))
                {
                    var temp = (Half)_union.ValueDouble;
                    value = Unsafe.As<Half, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(float))
                {
                    float temp = (float)_union.ValueDouble;
                    value = Unsafe.As<float, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(decimal))
                {
                    decimal temp = (decimal)_union.ValueDouble;
                    value = Unsafe.As<decimal, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(double))
                {
                    double temp = _union.ValueDouble;
                    value = Unsafe.As<double, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(sbyte))
                {
                    sbyte temp = (sbyte)_union.ValueDouble;
                    value = Unsafe.As<sbyte, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(byte))
                {
                    byte temp = (byte)_union.ValueDouble;
                    value = Unsafe.As<byte, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(short))
                {
                    short temp = (short)_union.ValueDouble;
                    value = Unsafe.As<short, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(ushort))
                {
                    ushort temp = (ushort)_union.ValueDouble;
                    value = Unsafe.As<ushort, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(int))
                {
                    int temp = (int)_union.ValueDouble;
                    value = Unsafe.As<int, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(uint))
                {
                    uint temp = (uint)_union.ValueDouble;
                    value = Unsafe.As<uint, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(long))
                {
                    long temp = (long)_union.ValueDouble;
                    value = Unsafe.As<long, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(ulong))
                {
                    ulong temp = (ulong)_union.ValueDouble;
                    value = Unsafe.As<ulong, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(Int128))
                {
                    var temp = (Int128)_union.ValueDouble;
                    value = Unsafe.As<Int128, T>(ref temp)!;
                    return true;
                }
                if (typeof(T) == typeof(UInt128))
                {
                    UInt128 temp = (UInt128)_union.ValueDouble;
                    value = Unsafe.As<UInt128, T>(ref temp)!;
                    return true;
                }
                return false;
            case LuauValueType.Boolean:
                if (typeof(T) == typeof(bool))
                {
                    bool temp = _union.ValueBool;
                    value = Unsafe.As<bool, T>(ref temp)!;
                    return true;
                }
                return false;
            case LuauValueType.Table:
                if (typeof(T) == typeof(LuauTable))
                {
                    var temp = new LuauTable(_state, _union.ValueReference);
                    value = Unsafe.As<LuauTable, T>(ref temp)!;
                    return true;
                }
                return false;
            case LuauValueType.Function:
                if (typeof(T) == typeof(LuauFunction))
                {
                    var temp = new LuauFunction(_state, _union.ValueReference);
                    value = Unsafe.As<LuauFunction, T>(ref temp)!;
                    return true;
                }
                return false;
            case LuauValueType.Buffer:
                if (typeof(T) == typeof(ReadOnlySpan<byte>))
                {
                    var temp = new LuauBuffer(_state, _union.ValueReference);
                    if (!temp.TryGetBytes(out ReadOnlySpan<byte> span))
                        return false;

                    value = Unsafe.As<ReadOnlySpan<byte>, T>(ref span)!;
                    return true;
                }
                if (typeof(T) == typeof(byte[]))
                {
                    var temp = new LuauBuffer(_state, _union.ValueReference);
                    if (!temp.TryGetBytes(out byte[] bytes))
                        return false;

                    value = Unsafe.As<byte[], T>(ref bytes)!;
                    return true;
                }
                if (typeof(T) == typeof(LuauBuffer))
                {
                    var temp = new LuauBuffer(_state, _union.ValueReference);
                    value = Unsafe.As<LuauBuffer, T>(ref temp)!;
                    return true;
                }
                return false;
            default:
                return false;
        }
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

    internal static unsafe LuauValue ToValue(LuauState state)
    {
        lua_State* L = state.L;
        var type = (lua_Type)lua_type(L, -1);
        switch (type)
        {
            case lua_Type.LUA_TNIL:
                return default;
            case lua_Type.LUA_TBOOLEAN:
                bool valueBool = lua_toboolean(L, -1) == 1;
                return new LuauValue(state, LuauValueType.Boolean, new LuauValueUnion(valueBool));
            case lua_Type.LUA_TNUMBER:
                double valueNumber = lua_tonumber(L, -1);
                return new LuauValue(state, LuauValueType.Number, new LuauValueUnion(valueNumber));
            case lua_Type.LUA_TSTRING:
                int referenceString = lua_ref(L, -1);
                return new LuauValue(state, LuauValueType.String, new LuauValueUnion(referenceString));
            case lua_Type.LUA_TTABLE:
                int referenceTable = lua_ref(L, -1);
                return new LuauValue(state, LuauValueType.Table, new LuauValueUnion(referenceTable));
            case lua_Type.LUA_TFUNCTION:
                int referenceFunction = lua_ref(L, -1);
                return new LuauValue(state, LuauValueType.Function, new LuauValueUnion(referenceFunction));
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Type switch
        {
            LuauValueType.Nil => "nil",
            LuauValueType.String => new LuauString(_state, _union.ValueReference).ToString(),
            LuauValueType.Number => _union.ValueDouble.ToString(CultureInfo.InvariantCulture),
            LuauValueType.Boolean => _union.ValueBool ? "true" : "false",
            LuauValueType.Table => new LuauTable(_state, _union.ValueReference).ToString(),
            LuauValueType.Function => new LuauFunction(_state, _union.ValueReference).ToString(),
            _ => "n/a",
        };
    }
}
