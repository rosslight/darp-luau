using System.Diagnostics.CodeAnalysis;

namespace Darp.Luau;

/// <summary>
/// Result of a managed Luau callback.
/// Either contains 0..4 <see cref="IntoLuau"/> values on success or an error message.
/// </summary>
public readonly ref struct LuauReturn
{
    private readonly IntoLuau _value1;
    private readonly IntoLuau _value2;
    private readonly IntoLuau _value3;
    private readonly IntoLuau _value4;
    private readonly int _valueCount;
    private readonly string? _error;

    public static readonly string NotHandledError = "__DARP_NOT_HANDLED__";

    private LuauReturn(
        int valueCount,
        IntoLuau value1 = default,
        IntoLuau value2 = default,
        IntoLuau value3 = default,
        IntoLuau value4 = default
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(valueCount, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(valueCount, 4);
        _value1 = value1;
        _value2 = value2;
        _value3 = value3;
        _value4 = value4;
        _valueCount = valueCount;
        _error = null;
    }

    private LuauReturn(string? error)
    {
        _value1 = default;
        _value2 = default;
        _value3 = default;
        _value4 = default;
        _valueCount = 0;
        _error = string.IsNullOrWhiteSpace(error) ? "something went wrong" : error;
    }

    public bool IsError => _error is not null;

    public int ValueCount => IsError ? 0 : _valueCount;

    public static LuauReturn Ok() => new(valueCount: 0);

    public static LuauReturn Ok(IntoLuau value) => new(valueCount: 1, value1: value);

    public static LuauReturn Ok(IntoLuau value1, IntoLuau value2) => new(valueCount: 2, value1: value1, value2: value2);

    public static LuauReturn Ok(IntoLuau value1, IntoLuau value2, IntoLuau value3) =>
        new(valueCount: 3, value1: value1, value2: value2, value3: value3);

    public static LuauReturn Ok(IntoLuau value1, IntoLuau value2, IntoLuau value3, IntoLuau value4) =>
        new(valueCount: 4, value1: value1, value2: value2, value3: value3, value4: value4);

    public static LuauReturn Error(string error) => new(error);

    public bool TryGetError([NotNullWhen(true)] out string? error)
    {
        error = _error;
        return IsError;
    }

    internal bool TryPushValues(LuauState state, out int outputCount, [NotNullWhen(false)] out string? error)
    {
        if (TryGetError(out error))
        {
            outputCount = 0;
            return false;
        }

        switch (_valueCount)
        {
            case 0:
                outputCount = 0;
                return true;
            case 1:
                _value1.Push(state);
                outputCount = 1;
                return true;
            case 2:
                _value1.Push(state);
                _value2.Push(state);
                outputCount = 2;
                return true;
            case 3:
                _value1.Push(state);
                _value2.Push(state);
                _value3.Push(state);
                outputCount = 3;
                return true;
            case 4:
                _value1.Push(state);
                _value2.Push(state);
                _value3.Push(state);
                _value4.Push(state);
                outputCount = 4;
                return true;
            default:
                throw new InvalidOperationException("Invalid number of return values.");
        }
    }
}
