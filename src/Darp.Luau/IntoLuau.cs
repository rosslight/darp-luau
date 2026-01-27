namespace Darp.Luau;

/// <summary> A helper struct to convert any value into a lua value </summary>
/// <remarks> Contains conversions for BCL methods, custom types have to define implicit operators themself </remarks>
public readonly ref struct IntoLuau
{
    private enum Kind : byte
    {
        // ReSharper disable once UnusedMember.Local -> Important for detecting a default struct
        Nil = 0,
        Bool,
        Number,
        Chars,
        Value,
    }

    private readonly Kind _type;
    private readonly bool _bool;
    private readonly double _number;
    private readonly ReadOnlySpan<char> _readOnlySpanChar;
    private readonly LuauValue _luauValue;

    private IntoLuau(bool value) => (_type, _bool) = (Kind.Bool, value);

    private IntoLuau(double value) => (_type, _number) = (Kind.Number, value);

    private IntoLuau(ReadOnlySpan<char> value)
    {
        _type = Kind.Chars;
        _readOnlySpanChar = value;
    }

    private IntoLuau(LuauValue value)
    {
        _type = Kind.Value;
        _luauValue = value;
    }

    /// <summary> Converts the underlying dotnet value to a luau value </summary>
    /// <param name="lua"> The luau state to create the value for </param>
    /// <returns> The luau value </returns>
    /// <remarks> This method might change the lua stack! Use with care </remarks>
    internal LuauValue Into(LuauState lua) =>
        _type switch
        {
            Kind.Chars => lua.CreateString(_readOnlySpanChar),
            Kind.Bool => _bool,
            Kind.Number => _number,
            Kind.Value => _luauValue,
            _ => default,
        };

    public static implicit operator IntoLuau(string value) => new(value);

    public static implicit operator IntoLuau(ReadOnlySpan<char> value) => new(value);

    public static implicit operator IntoLuau(bool value) => new(value);

    public static implicit operator IntoLuau(double value) => new(value);

    public static implicit operator IntoLuau(LuauValue value) => new(value);
}
