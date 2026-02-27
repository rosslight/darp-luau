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
        Buffer,
        UserdataFactory,
    }

    /// <summary> Describes of which kind the resulting <see cref="LuauValue"/> will be </summary>
    internal Kind Type { get; }
    private readonly bool _bool;
    private readonly double _number;
    private readonly int _integer;
    private readonly ReadOnlySpan<char> _readOnlySpanChar;
    private readonly ReadOnlySpan<byte> _readOnlyBuffer;
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

    private IntoLuau(ReadOnlySpan<byte> valueChars)
    {
        Type = Kind.Buffer;
        _readOnlyBuffer = valueChars;
    }

    private IntoLuau(LuauValue value)
    {
        Type = Kind.Value;
        _luauValue = value;
    }

    private IntoLuau(Func<LuauState, LuauUserdata> factory)
    {
        Type = Kind.UserdataFactory;
        _factory = factory;
    }

    internal unsafe void Push(LuauState state)
    {
        lua_State* L = state.L;
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
            case Kind.Buffer:
                void* pDest = lua_newbuffer(L, (nuint)_readOnlyBuffer.Length);
                var destination = new Span<byte>(pDest, _readOnlyBuffer.Length);
                _readOnlyBuffer.CopyTo(destination);
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
                LuauUserdata userdata = _factory.Invoke(state);
                if (
                    userdata.State is null
                    || userdata.Reference is 0
                    || !userdata.State.ReferenceTracker.HasRegistryReference(userdata.Reference)
                )
                {
                    lua_pushnil(L);
                    break;
                }

                try
                {
                    ((LuauValue)userdata).Push(L);
                }
                finally
                {
                    userdata.Dispose();
                }
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

    /// <summary> Converts to a buffer </summary>
    /// <param name="value"> The byte array </param>
    /// <returns> A temporary representation of the value </returns>
    public static implicit operator IntoLuau(byte[] value) => new(value);

    /// <summary> Converts to a userdata </summary>
    /// <param name="userdata"> The userdata </param>
    /// <typeparam name="T"> The type of the userdata </typeparam>
    /// <returns> A temporary representation of the value </returns>
    public static IntoLuau FromUserdata<T>(T userdata)
        where T : class, ILuauUserData<T> => new(state => state.GetOrCreateUserdata(userdata));

    /// <summary> Converts to a userdata </summary>
    /// <param name="factory"> The userdata factory </param>
    /// <returns> A temporary representation of the value </returns>
    public static IntoLuau FromUserdata(Func<LuauState, LuauUserdata> factory) => new(factory);
}
