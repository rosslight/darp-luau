namespace Darp.Luau.Generator;

internal enum LuauValueType
{
    Boolean, // bool

    String, // -> ReadOnlySpan<byte>
    StringCharSpan, // -> ReadOnlySpan<char>
    StringString, // -> string

    Number, // -> double
    NumberByte, // -> byte
    NumberUShort, // -> ushort
    NumberUInt, // -> uint
    NumberULong, // -> ulong
    NumberUInt128, // -> System.UInt128
    NumberSByte, // -> sbyte
    NumberShort, // -> short
    NumberInt, // -> int
    NumberLong, // -> long
    NumberInt128, // -> System.Int128
    NumberHalf, // -> System.Half
    NumberFloat, // -> float
    NumberDecimal, // -> decimal

    LuauValue,
    LuauTable,
    LuauFunction,
    LuauString,

    LuauBuffer, // -> ReadOnlySpan<byte>
}
