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
        Chars,
        Value,
    }

    /// <summary> Describes of which kind the resulting <see cref="LuauValue"/> will be </summary>
    internal Kind Type { get; }
    private readonly bool _bool;
    private readonly double _number;
    private readonly ReadOnlySpan<char> _readOnlySpanChar;
    private readonly LuauValue _luauValue;

    private IntoLuau(bool value) => (Type, _bool) = (Kind.Bool, value);

    private IntoLuau(double value) => (Type, _number) = (Kind.Number, value);

    private IntoLuau(ReadOnlySpan<char> value)
    {
        Type = Kind.Chars;
        _readOnlySpanChar = value;
    }

    private IntoLuau(LuauValue value)
    {
        Type = Kind.Value;
        _luauValue = value;
    }

    /// <summary> Converts the underlying dotnet value to a luau value </summary>
    /// <param name="lua"> The luau state to create the value for </param>
    /// <returns> The luau value </returns>
    /// <remarks> This method might change the lua stack! Use with care </remarks>
    internal LuauValue Into(LuauState lua) =>
        Type switch
        {
            // TODO: Find out how to cleanup the
            Kind.Chars => lua.CreateString(_readOnlySpanChar),
            Kind.Bool => _bool,
            Kind.Number => _number,
            Kind.Value => _luauValue,
            _ => default,
        };

    public static implicit operator IntoLuau(string? value) => value is null ? default : new IntoLuau(value);

    public static implicit operator IntoLuau(ReadOnlySpan<char> value) => value.IsEmpty ? default : new IntoLuau(value);

    public static implicit operator IntoLuau(bool? value) => value is null ? default : new IntoLuau(value.Value);

    public static implicit operator IntoLuau(double? value) => value is null ? default : new IntoLuau(value.Value);

    public static implicit operator IntoLuau(LuauValue value) => new(value);
}
