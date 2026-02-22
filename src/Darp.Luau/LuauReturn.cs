using System.Diagnostics.CodeAnalysis;

namespace Darp.Luau;

/// <summary>
/// Represents the return value of a managed Luau callback.
/// Use <see cref="Ok()"/> or one of the <see cref="Ok(IntoLuau)"/> overloads for successful results,
/// or <see cref="Error(string)"/> to return an error.
/// </summary>
/// <remarks>
/// The default value represents an error with message <c>Unknown error</c>.
/// </remarks>
public readonly ref struct LuauReturn
{
    private readonly IntoLuauBuffer _buffer;
    private readonly string? _error;

    /// <summary> Gets whether this callback result is successful. </summary>
    public bool IsOk { get; }

    /// <summary> Used to indicate that a callback intentionally did not handle a request. </summary>
    internal const string NotHandled = "__DARP_NOT_HANDLED__";

    private LuauReturn(
        int valueCount,
        IntoLuau value1 = default,
        IntoLuau value2 = default,
        IntoLuau value3 = default,
        IntoLuau value4 = default
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(valueCount, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(valueCount, IntoLuauBuffer.MaxLength);
        _buffer = new IntoLuauBuffer(valueCount, value1, value2, value3, value4);
        IsOk = true;
    }

    private LuauReturn(string error)
    {
        IsOk = false;
        _error = error;
    }

    /// <summary> Creates a successful callback result with no return values. </summary>
    public static LuauReturn Ok() => new(valueCount: 0);

    /// <summary> Creates a successful callback result with one return value. </summary>
    /// <param name="value">The value to return to Luau.</param>
    public static LuauReturn Ok(IntoLuau value) => new(valueCount: 1, value);

    /// <summary> Creates a successful callback result with two return values. </summary>
    /// <param name="value1">The first value to return to Luau.</param>
    /// <param name="value2">The second value to return to Luau.</param>
    public static LuauReturn Ok(IntoLuau value1, IntoLuau value2) => new(valueCount: 2, value1, value2);

    /// <summary> Creates a successful callback result with three return values. </summary>
    /// <param name="value1">The first value to return to Luau.</param>
    /// <param name="value2">The second value to return to Luau.</param>
    /// <param name="value3">The third value to return to Luau.</param>
    public static LuauReturn Ok(IntoLuau value1, IntoLuau value2, IntoLuau value3) =>
        new(valueCount: 3, value1, value2, value3);

    /// <summary> Creates a successful callback result with four return values. </summary>
    /// <param name="value1">The first value to return to Luau.</param>
    /// <param name="value2">The second value to return to Luau.</param>
    /// <param name="value3">The third value to return to Luau.</param>
    /// <param name="value4">The fourth value to return to Luau.</param>
    public static LuauReturn Ok(IntoLuau value1, IntoLuau value2, IntoLuau value3, IntoLuau value4) =>
        new(valueCount: 4, value1, value2, value3, value4);

    /// <summary> Creates a failed callback result with an error message. </summary>
    /// <param name="error">Error message reported to the caller.</param>
    /// <remarks>When the provided text is empty or whitespace, <c>Unknown error</c> is used.</remarks>
    public static LuauReturn Error(string error) => new(error);

    /// <summary>
    /// Creates a callback result that signals the member or method is not handled.
    /// </summary>
    public static LuauReturn NotHandledError => Error(NotHandled);

    /// <summary> Pushes return values when this result is successful. </summary>
    /// <param name="state">Target state that receives the return values.</param>
    /// <param name="outputCount">Number of values produced for the callback.</param>
    /// <param name="error">Receives the error message when this result is not successful.</param>
    /// <returns><c>true</c> when values are available; otherwise <c>false</c>.</returns>
    internal bool TryPushValues(LuauState state, out int outputCount, [NotNullWhen(false)] out string? error)
    {
        if (!IsOk)
        {
            outputCount = 0;
            error = _error ?? "Unknown error";
            return false;
        }

        error = null;
        outputCount = _buffer.Length;
        switch (outputCount)
        {
            case 0:
                return true;
            case 1:
                _buffer.Element0.Push(state);
                return true;
            case 2:
                _buffer.Element0.Push(state);
                _buffer.Element1.Push(state);
                return true;
            case 3:
                _buffer.Element0.Push(state);
                _buffer.Element1.Push(state);
                _buffer.Element2.Push(state);
                return true;
            case 4:
                _buffer.Element0.Push(state);
                _buffer.Element1.Push(state);
                _buffer.Element2.Push(state);
                _buffer.Element3.Push(state);
                return true;
            default:
                throw new InvalidOperationException("Invalid number of return values.");
        }
    }

    private ref struct IntoLuauBuffer(
        int length,
        IntoLuau element0,
        IntoLuau element1,
        IntoLuau element2,
        IntoLuau element3
    )
    {
        public const int MaxLength = 4;

        public readonly int Length = length;
        public readonly IntoLuau Element0 = element0;
        public readonly IntoLuau Element1 = element1;
        public readonly IntoLuau Element2 = element2;
        public readonly IntoLuau Element3 = element3;
    }
}
