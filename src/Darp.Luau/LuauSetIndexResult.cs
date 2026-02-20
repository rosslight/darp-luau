using System.Diagnostics.CodeAnalysis;

namespace Darp.Luau;

/// <summary>
/// Result of a managed userdata <c>__newindex</c> callback.
/// Either handled, not-handled, or an error message.
/// </summary>
public readonly struct LuauSetIndexResult
{
    private readonly ResultKind _kind;
    private readonly string? _error;

    private LuauSetIndexResult(ResultKind kind, string? error = null)
    {
        _kind = kind;
        _error = error;
    }

    public bool IsHandled => _kind is ResultKind.Handled;

    public bool IsNotHandled => _kind is ResultKind.NotHandled;

    public bool IsError => _kind is ResultKind.Error;

    public static LuauSetIndexResult Handled => new(ResultKind.Handled);

    public static LuauSetIndexResult NotHandled => default;

    public static LuauSetIndexResult Error(string? error) =>
        new(ResultKind.Error, string.IsNullOrWhiteSpace(error) ? "something went wrong" : error);

    public bool TryGetError([NotNullWhen(true)] out string? error)
    {
        error = _error;
        return IsError;
    }

    public static implicit operator LuauSetIndexResult(string error) => Error(error);

    private enum ResultKind
    {
        // NotHandled has to be 0 -> default value
        NotHandled = 0,
        Handled = 1,
        Error = 2,
    }
}
