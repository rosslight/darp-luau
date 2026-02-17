using System.Diagnostics;
using System.Text;
using Darp.Luau.Native;
using static Darp.Luau.Native.LuauNative;

namespace Darp.Luau;

/// <summary> A helper struct to convert any value into a lua value </summary>
/// <remarks> Contains conversions for BCL methods, custom types have to define implicit operators themselves </remarks>
public readonly ref struct IntoLuau
{
    internal enum Kind
    {
        // ReSharper disable once UnusedMember.Local -> Important for detecting a default struct
        Nil = 0,
        Bool,
        Number,
        Integer,
        Unsigned,
        Chars,
        Value,
        UserdataFactory,
    }

    /// <summary> Describes of which kind the resulting <see cref="LuauValue"/> will be </summary>
    internal Kind Type { get; }
    private readonly bool _bool;
    private readonly double _number;
    private readonly int _integer;
    private readonly ReadOnlySpan<char> _readOnlySpanChar;
    private readonly LuauValue _luauValue;
    private readonly Func<LuauState, LuauUserdata>? _factory;

    private IntoLuau(bool valueBool) => (Type, _bool) = (Kind.Bool, valueBool);

    private IntoLuau(double valueNumber) => (Type, _number) = (Kind.Number, valueNumber);

    private IntoLuau(int valueInteger) => (Type, _integer) = (Kind.Integer, valueInteger);

    private IntoLuau(uint valueUnsigned) => (Type, _integer) = (Kind.Unsigned, (int)valueUnsigned);

    private IntoLuau(ReadOnlySpan<char> valueChars)
    {
        Type = Kind.Chars;
        _readOnlySpanChar = valueChars;
    }

    private IntoLuau(LuauValue value)
    {
        Type = Kind.Value;
        _luauValue = value;
    }

    private IntoLuau(Func<LuauState, LuauUserdata> factory)
    {
        Type = Kind.Value;
        _factory = factory;
    }

    internal unsafe void Push(lua_State* L)
    {
        switch (Type)
        {
            case Kind.Chars:
                if (_readOnlySpanChar.Length > 256)
                {
                    Span<byte> buffer = new byte[Encoding.UTF8.GetByteCount(_readOnlySpanChar)];
                    int numberOfBytes = Encoding.UTF8.GetBytes(_readOnlySpanChar, buffer);
                    fixed (byte* pStr = buffer[..numberOfBytes])
                    {
                        lua_pushlstring(L, pStr, (nuint)numberOfBytes);
                    }
                }
                else
                {
                    Span<byte> buffer = stackalloc byte[Encoding.UTF8.GetByteCount(_readOnlySpanChar)];
                    int numberOfBytes = Encoding.UTF8.GetBytes(_readOnlySpanChar, buffer);
                    fixed (byte* pStr = buffer[..numberOfBytes])
                    {
                        lua_pushlstring(L, pStr, (nuint)numberOfBytes);
                    }
                }
                break;
            case Kind.Bool:
                lua_pushboolean(L, _bool ? 1 : 0);
                break;
            case Kind.Number:
                lua_pushnumber(L, _number);
                break;
            case Kind.Integer:
                lua_pushinteger(L, _integer);
                break;
            case Kind.Unsigned:
                lua_pushunsigned(L, (uint)_integer);
                break;
            case Kind.Value:
                _luauValue.Push(L);
                break;
            case Kind.UserdataFactory:
                Debug.Assert(_factory is not null);
                _factory.Invoke(null!);
                break;
            case Kind.Nil:
            default:
                lua_pushnil(L);
                break;
        }
    }

    /// <summary> Converts to a string or nil </summary>
    /// <param name="value"> The string value </param>
    /// <returns> A temporary representation of the value </returns>
    public static implicit operator IntoLuau(string? value) =>
        value is null ? default : new IntoLuau(valueChars: value);

    /// <summary> Converts to a string or nil </summary>
    /// <param name="value"> The string value </param>
    /// <returns> A temporary representation of the value </returns>
    public static implicit operator IntoLuau(ReadOnlySpan<char> value) =>
        value.IsEmpty ? default : new IntoLuau(valueChars: value);

    /// <summary> Converts to a boolean </summary>
    /// <param name="value"> The boolean value </param>
    /// <returns> A temporary representation of the value </returns>
    public static implicit operator IntoLuau(bool value) => new(valueBool: value);

    /// <summary> Converts to a boolean or nil </summary>
    /// <param name="value"> The boolean value </param>
    /// <returns> A temporary representation of the value </returns>
    public static implicit operator IntoLuau(bool? value) =>
        value is null ? default : new IntoLuau(valueBool: value.Value);

    /// <summary> Converts to a number </summary>
    /// <param name="value"> The number value </param>
    /// <returns> A temporary representation of the value </returns>
    public static implicit operator IntoLuau(Half value) => new(valueNumber: (double)value);

    /// <summary> Converts to a number </summary>
    /// <param name="value"> The number value </param>
    /// <returns> A temporary representation of the value </returns>
    public static implicit operator IntoLuau(Half? value) =>
        value is null ? default : new IntoLuau(valueNumber: (double)value.Value);

    /// <summary> Converts to a number </summary>
    /// <param name="value"> The number value </param>
    /// <returns> A temporary representation of the value </returns>
    public static implicit operator IntoLuau(double value) => new(valueNumber: value);

    /// <summary> Converts to an unsigned integer </summary>
    /// <param name="value"> The unsigned integer value </param>
    /// <returns> A temporary representation of the value </returns>
    public static implicit operator IntoLuau(byte value) => new(valueUnsigned: value);

    /// <summary> Converts to an unsigned integer </summary>
    /// <param name="value"> The unsigned integer value </param>
    /// <returns> A temporary representation of the value </returns>
    public static implicit operator IntoLuau(ushort value) => new(valueUnsigned: value);

    /// <summary> Converts to an unsigned integer </summary>
    /// <param name="value"> The unsigned integer value </param>
    /// <returns> A temporary representation of the value </returns>
    public static implicit operator IntoLuau(uint value) => new(valueUnsigned: value);

    /// <summary> Converts to an integer </summary>
    /// <param name="value"> The integer value </param>
    /// <returns> A temporary representation of the value </returns>
    public static implicit operator IntoLuau(int value) => new(valueInteger: value);

    /// <summary> Converts to a number or nil </summary>
    /// <param name="value"> The number value </param>
    /// <returns> A temporary representation of the value </returns>
    public static implicit operator IntoLuau(double? value) =>
        value is null ? default : new IntoLuau(valueNumber: value.Value);

    /// <summary> Converts to an unsigned integer </summary>
    /// <param name="value"> The unsigned integer value </param>
    /// <returns> A temporary representation of the value </returns>
    public static implicit operator IntoLuau(byte? value) =>
        value is null ? default : new IntoLuau(valueUnsigned: value.Value);

    /// <summary> Converts to an unsigned integer </summary>
    /// <param name="value"> The unsigned integer value </param>
    /// <returns> A temporary representation of the value </returns>
    public static implicit operator IntoLuau(ushort? value) =>
        value is null ? default : new IntoLuau(valueUnsigned: value.Value);

    /// <summary> Converts to an unsigned integer </summary>
    /// <param name="value"> The unsigned integer value </param>
    /// <returns> A temporary representation of the value </returns>
    public static implicit operator IntoLuau(uint? value) =>
        value is null ? default : new IntoLuau(valueUnsigned: value.Value);

    /// <summary> Converts to an integer </summary>
    /// <param name="value"> The integer value </param>
    /// <returns> A temporary representation of the value </returns>
    public static implicit operator IntoLuau(int? value) =>
        value is null ? default : new IntoLuau(valueInteger: value.Value);

    /// <summary> Converts to a Luau value </summary>
    /// <param name="value"> The Luau value </param>
    /// <returns> A temporary representation of the value </returns>
    public static implicit operator IntoLuau(LuauValue value) => new(value);

    public static IntoLuau FromUserdata(Func<LuauState, LuauUserdata> factory) => new(factory);
}
