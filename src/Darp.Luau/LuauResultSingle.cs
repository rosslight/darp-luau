using System.Diagnostics.CodeAnalysis;

namespace Darp.Luau;

/// <summary>
/// Result of a managed userdata <c>__index</c> callback.
/// Either contains one <see cref="IntoLuau"/> value, an error message, or not-handled state.
/// </summary>
public readonly ref struct LuauResultSingle
{
    private readonly ResultKind _kind;
    private readonly IntoLuau _value;
    private readonly string? _error;

    private LuauResultSingle(ResultKind kind, IntoLuau value = default, string? error = null)
    {
        _kind = kind;
        _value = value;
        _error = error;
    }

    public bool IsNotHandled => _kind is ResultKind.NotHandled;

    public bool IsError => _kind is ResultKind.Error;

    public static LuauResultSingle NotHandled => default;

    public static LuauResultSingle Value(IntoLuau value) => new(ResultKind.Value, value: value);

    public static LuauResultSingle Error(string? error) =>
        new(ResultKind.Error, error: string.IsNullOrWhiteSpace(error) ? "something went wrong" : error);

    public bool TryGetValue(out IntoLuau value)
    {
        value = _value;
        return _kind is ResultKind.Value;
    }

    public bool TryGetError([NotNullWhen(true)] out string? error)
    {
        error = _error;
        return IsError;
    }

    public static implicit operator LuauResultSingle(IntoLuau value) => Value(value);

    public static implicit operator LuauResultSingle(string error) => Error(error);

    private enum ResultKind
    {
        NotHandled = 0,
        Value = 1,
        Error = 2,
    }
}
