using System.Diagnostics.CodeAnalysis;
using System.Text;
using Darp.Luau.Internal;
using Darp.Luau.Native;

namespace Darp.Luau;

/// <summary>
/// Represents an owned Luau string reference stored in the registry.
/// </summary>
public readonly struct LuauString : IDisposable
{
    private readonly LuauRefSource _source;

    internal LuauState? State => _source.State;
    internal int Reference => _source.Reference;

    /// <summary> Do not initialize directly. Create via <see cref="LuauState"/> APIs. </summary>
    [Obsolete("Do not initialize the LuauString. Create using the LuauState instead", true)]
    public LuauString() { }

    internal LuauString(LuauState? state, int reference)
    {
        _source = LuauRefSource.FromReference(state, reference, lua_Type.LUA_TSTRING);
    }

    /// <summary>
    /// Attempts to get the underlying string bytes as a UTF-8 span.
    /// </summary>
    /// <param name="value">Receives a span over Luau-owned memory when successful.</param>
    /// <returns><c>true</c> when the string is still valid; otherwise <c>false</c>.</returns>
    public bool TryGet(out ReadOnlySpan<byte> value) =>
        LuauStringAccessCore.TryGet(_source, nameof(LuauString), out value);

    /// <summary>
    /// Attempts to decode the underlying UTF-8 bytes into a managed <see cref="string"/>.
    /// </summary>
    /// <param name="value">Receives the decoded string when successful.</param>
    /// <returns><c>true</c> when decoding succeeded; otherwise <c>false</c>.</returns>
    public bool TryGet([NotNullWhen(true)] out string? value) =>
        LuauStringAccessCore.TryGet(_source, nameof(LuauString), out value);

    /// <summary> Releases this string reference from the state registry. </summary>
    public void Dispose() => _source.Dispose();

    /// <summary> Converts this string reference into an <see cref="IntoLuau"/> value. </summary>
    public static implicit operator IntoLuau(LuauString value) => IntoLuau.FromRefSource(value._source);

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
