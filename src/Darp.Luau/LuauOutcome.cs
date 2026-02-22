using System.Diagnostics.CodeAnalysis;

namespace Darp.Luau;

/// <summary>
/// Represents the outcome of a managed userdata <c>__newindex</c> callback.
/// Use <see cref="Ok"/> when assignment was handled,
/// <see cref="NotHandledError"/> for unknown members,
/// or <see cref="Error(string)"/> to report an error.
/// </summary>
/// <remarks>
/// The default value represents an error with message <c>Unknown error</c>.
/// </remarks>
public readonly ref struct LuauOutcome
{
    private readonly string? _error;

    /// <summary> Gets whether this callback result is successful. </summary>
    public bool IsOk { get; }

    private LuauOutcome(bool isOk, string? error = null)
    {
        IsOk = isOk;
        _error = error;
    }

    /// <summary> Creates a successful callback result that indicates the assignment was handled. </summary>
    public static LuauOutcome Ok => new(isOk: true);

    /// <summary> Creates a callback result that signals the member assignment is not handled. </summary>
    public static LuauOutcome NotHandledError => Error(LuauReturn.NotHandled);

    /// <summary> Creates a failed callback result with an error message. </summary>
    /// <param name="error">Error message reported to the caller.</param>
    /// <remarks>When the provided text is empty or whitespace, <c>Unknown error</c> is used.</remarks>
    public static LuauOutcome Error(string error) => new(isOk: false, error: error);

    /// <summary> Gets the error message when this result is not successful. </summary>
    /// <param name="error">Receives the error message when <see cref="IsOk"/> is <c>false</c>.</param>
    /// <returns><c>true</c> when an error is present; otherwise <c>false</c>.</returns>
    public bool TryGetError([NotNullWhen(true)] out string? error)
    {
        if (IsOk)
        {
            error = null;
            return false;
        }

        error = _error ?? "Unknown error";
        return true;
    }
}
