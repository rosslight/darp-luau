using System.Diagnostics.CodeAnalysis;
using System.Text;
using Darp.Luau.Internal;
using Darp.Luau.Native;
using Darp.Luau.Utils;

namespace Darp.Luau;

/// <summary>
/// Represents a borrowed, stack-bound Luau string value read from callback arguments.
/// </summary>
/// <remarks>
/// This view does not own a registry reference.
/// It is valid only while the originating callback frame is active on the same <see cref="LuauState"/>.
/// Using it after the callback frame ends throws <see cref="ObjectDisposedException"/>.
/// </remarks>
public readonly ref struct LuauStringView
{
    private readonly StackReference _reference;

    internal LuauStringView(LuauState state, int stackIndex) => _reference = new StackReference(state, stackIndex);

    /// <summary>
    /// Attempts to get the underlying string bytes as a UTF-8 span.
    /// </summary>
    /// <param name="value">Receives a span over Luau-owned memory when successful.</param>
    /// <returns><c>true</c> when the string is still valid; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// The returned span aliases Luau memory and should be consumed immediately.
    /// Copy the data if it must outlive the callback frame.
    /// </remarks>
    public bool TryGet(out ReadOnlySpan<byte> value) => LuauStringAccessCore.TryGet(_reference, out value);

    /// <summary>
    /// Attempts to decode the underlying UTF-8 bytes into a managed <see cref="string"/>.
    /// </summary>
    /// <param name="value">Receives the decoded string when successful.</param>
    /// <returns><c>true</c> when decoding succeeded; otherwise <c>false</c>.</returns>
    public bool TryGet([NotNullWhen(true)] out string? value) => LuauStringAccessCore.TryGet(_reference, out value);

    /// <summary>
    /// Converts this borrowed string view to an <see cref="IntoLuau"/> value without creating an owned reference.
    /// </summary>
    /// <param name="value">The borrowed string view.</param>
    /// <returns>A temporary representation with the same callback-frame lifetime constraints.</returns>
    public static implicit operator IntoLuau(LuauStringView value) => IntoLuau.FromRefSource(value._reference);

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
