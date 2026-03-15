using System.Diagnostics.CodeAnalysis;
using System.Text;
using Darp.Luau.Internal;
using Darp.Luau.Native;
using Darp.Luau.Utils;

namespace Darp.Luau;

/// <summary>
/// Represents an owned Luau string reference stored in the registry.
/// </summary>
public readonly struct LuauString : IDisposable
{
    private readonly LuauState? _state;
    private readonly ulong _handle;

    /// <summary> True, if the <see cref="LuauTable"/> refers to a valid lua ref; False, otherwise </summary>
    public bool IsDisposed => !_state.IsReferenceValid(_handle);

    /// <summary> Do not initialize directly. Create via <see cref="LuauState"/> APIs. </summary>
    [Obsolete("Do not initialize the LuauString. Create using the LuauState instead", true)]
    public LuauString() { }

    internal LuauString(LuauState state, ulong handle)
    {
        _state = state;
        _handle = handle;
    }

    /// <summary>
    /// Attempts to get the underlying string bytes as a UTF-8 span.
    /// </summary>
    /// <param name="value">Receives a span over Luau-owned memory when successful.</param>
    /// <returns><c>true</c> when the string is still valid; otherwise <c>false</c>.</returns>
    public bool TryGet(out ReadOnlySpan<byte> value)
    {
        value = default;
        return _state.TryGetTrackedReference(_handle, out RegistryReferenceTracker.TrackedReference? reference)
            && LuauStringAccessCore.TryGet(reference, out value);
    }

    /// <summary>
    /// Attempts to decode the underlying UTF-8 bytes into a managed <see cref="string"/>.
    /// </summary>
    /// <param name="value">Receives the decoded string when successful.</param>
    /// <returns><c>true</c> when decoding succeeded; otherwise <c>false</c>.</returns>
    public bool TryGet([NotNullWhen(true)] out string? value)
    {
        value = null;
        return _state.TryGetTrackedReference(_handle, out RegistryReferenceTracker.TrackedReference? reference)
            && LuauStringAccessCore.TryGet(reference, out value);
    }

    /// <summary> Releases this string reference from the state registry. </summary>
    public void Dispose() => _state?.ReferenceTracker.ReleaseRef(_handle);

    /// <summary> Converts this string reference into an <see cref="IntoLuau"/> value. </summary>
    public static implicit operator IntoLuau(LuauString value) => IntoLuau.Borrow(value._state, value._handle);

    /// <summary> Transfers ownership of this string reference into a <see cref="LuauValue"/>. </summary>
    /// <remarks>
    /// This method consumes the current <see cref="LuauString"/>.
    /// It does not clone ownership, so using this wrapper afterwards is invalid.
    /// </remarks>
    /// <returns>The same underlying reference represented as a <see cref="LuauValue"/>.</returns>
    public LuauValue DisposeAndToLuauValue() => LuauValue.Move(_state, _handle, LuauValueType.String);

    /// <inheritdoc />
    public override string ToString()
    {
        try
        {
            return TryGet(out string? value) ? value : "<nil>";
        }
        catch (ObjectDisposedException)
        {
            return "<nil>";
        }
    }
}
