using Darp.Luau.Internal;
using Darp.Luau.Native;

namespace Darp.Luau;

public readonly struct LuauBuffer : IDisposable
{
    private readonly LuauRefSource _source;

    internal LuauState? State => _source.State;
    internal int Reference => _source.Reference;

    [Obsolete("Do not initialize the LuauBuffer. Create using the LuauState instead", true)]
    public LuauBuffer() { }

    internal LuauBuffer(LuauState? state, int reference)
    {
        _source = LuauRefSource.FromReference(state, reference, lua_Type.LUA_TBUFFER);
    }

    /// <summary> Ability for <see cref="LuauBuffer"/> to be passed into functions that accept <see cref="IntoLuau"/> </summary>
    /// <param name="value"> The buffer </param>
    /// <returns> The converted value </returns>
    public static implicit operator IntoLuau(LuauBuffer value) => IntoLuau.FromRefSource(value._source);

    /// <summary>
    /// Attempts to get a read-only span over the underlying Luau buffer bytes.
    /// </summary>
    /// <param name="bytes">Receives a span over Luau-owned memory when successful.</param>
    /// <returns><c>true</c> when the buffer is still valid; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// The returned span aliases Luau memory and should be consumed immediately.
    /// Copy the data if it must outlive the callback frame.
    /// </remarks>
    public bool TryGet(out ReadOnlySpan<byte> bytes) =>
        LuauBufferAccessCore.TryGet(_source, nameof(LuauBuffer), out bytes);

    /// <summary>
    /// Attempts to copy the underlying Luau buffer bytes into a managed array.
    /// </summary>
    /// <param name="bytes">Receives the copied bytes when successful; otherwise an empty array.</param>
    /// <returns><c>true</c> when the copy succeeded; otherwise <c>false</c>.</returns>
    public bool TryGet(out byte[] bytes) => LuauBufferAccessCore.TryGet(_source, nameof(LuauBuffer), out bytes);

    /// <inheritdoc />
    public override string ToString() => TryGet(out ReadOnlySpan<byte> span) ? Convert.ToHexString(span) : "<nil>";

    /// <summary> Remove the reference from the lua state </summary>
    public void Dispose() => _source.Dispose();
}
