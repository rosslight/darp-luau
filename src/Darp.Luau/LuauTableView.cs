using Darp.Luau.Internal;
using Darp.Luau.Utils;

namespace Darp.Luau;

/// <summary> Represents a borrowed, stack-bound Luau table value read from callback arguments. </summary>
/// <remarks>
/// This view does not own a registry reference.
/// It is valid only while the originating callback frame is active on the same <see cref="LuauState"/>.
/// Using it after the callback frame ends throws <see cref="ObjectDisposedException"/>.
/// </remarks>
public readonly ref struct LuauTableView : ILuauView<LuauTable>
{
    private readonly StackReference _reference;

    internal LuauTableView(LuauState state, int stackIndex) => _reference = new StackReference(state, stackIndex);

    /// <summary>Gets the count of the table if viewed as a list.</summary>
    /// <remarks>If a lua table has holes, this property is unreliable.</remarks>
    public int ListCount => LuauTableAccessCore.ListCount(_reference);

    /// <summary>Sets a value on the borrowed table.</summary>
    public void Set(IntoLuau key, IntoLuau value) => LuauTableAccessCore.Set(_reference, key, value);

    /// <summary>Determines whether <paramref name="key"/> resolves to a non-<c>nil</c> value.</summary>
    public bool ContainsKey(IntoLuau key) => LuauTableAccessCore.ContainsKey(_reference, key);

    ///<inheritdoc/>
    public LuauTable ToOwned()
    {
        LuauState state = _reference.ValidateInternal();
        return new LuauTable(state, ReferenceSourceExtensions.ToOwnedHandle(_reference));
    }

    /// <summary>
    /// Converts this borrowed table view to an <see cref="IntoLuau"/> value without creating an owned reference.
    /// </summary>
    public static implicit operator IntoLuau(LuauTableView value) => IntoLuau.FromRefSource(value._reference);

    /// <inheritdoc />
    public override string ToString() => _reference.ToString();
}
