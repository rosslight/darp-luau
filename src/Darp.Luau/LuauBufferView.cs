using Darp.Luau.Internal;
using Darp.Luau.Utils;

namespace Darp.Luau;

/// <summary>
/// Represents a borrowed, stack-bound Luau buffer value read from callback arguments.
/// </summary>
/// <remarks>
/// This view does not own a registry reference.
/// It is valid only while the originating callback frame is active on the same <see cref="LuauState"/>.
/// Using it after the callback frame ends throws <see cref="ObjectDisposedException"/>.
/// </remarks>
public readonly ref struct LuauBufferView : ILuauView<LuauBuffer>
{
    private readonly StackReference _reference;

    internal LuauBufferView(LuauState state, int stackIndex) => _reference = new StackReference(state, stackIndex);

    /// <summary>
    /// Attempts to get a read-only span over the underlying Luau buffer bytes.
    /// </summary>
    /// <param name="bytes">Receives a span over Luau-owned memory when successful.</param>
    /// <returns><c>true</c> when the buffer is still valid; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// The returned span aliases Luau memory and should be consumed immediately.
    /// Copy the data if it must outlive the callback frame.
    /// </remarks>
    public bool TryGet(out ReadOnlySpan<byte> bytes) => LuauBufferAccessCore.TryGet(_reference, out bytes);

    /// <summary>
    /// Attempts to copy the underlying Luau buffer bytes into a managed array.
    /// </summary>
    /// <param name="bytes">Receives the copied bytes when successful; otherwise an empty array.</param>
    /// <returns><c>true</c> when the copy succeeded; otherwise <c>false</c>.</returns>
    public bool TryGet(out byte[] bytes) => LuauBufferAccessCore.TryGet(_reference, out bytes);

    /// <inheritdoc/>
    public LuauBuffer ToOwned()
    {
        LuauState state = _reference.ValidateInternal();
        return new LuauBuffer(state, ReferenceSourceExtensions.ToOwnedHandle(_reference));
    }

    /// <summary>
    /// Converts this borrowed buffer view to an <see cref="IntoLuau"/> value without creating an owned reference.
    /// </summary>
    /// <param name="value">The borrowed buffer view.</param>
    /// <returns>A temporary representation with the same callback-frame lifetime constraints.</returns>
    public static implicit operator IntoLuau(LuauBufferView value) => IntoLuau.FromRefSource(value._reference);

    /// <inheritdoc />
    public override string ToString() => TryGet(out ReadOnlySpan<byte> span) ? Convert.ToHexString(span) : "<nil>";
}
