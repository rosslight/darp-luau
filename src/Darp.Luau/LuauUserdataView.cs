using System.Diagnostics.CodeAnalysis;
using Darp.Luau.Internal;
using Darp.Luau.Native;

namespace Darp.Luau;

/// <summary>
/// Represents a borrowed, stack-bound Luau userdata value read from callback arguments.
/// </summary>
/// <remarks>
/// This view does not own a registry reference.
/// It is valid only while the originating callback frame is active on the same <see cref="LuauState"/>.
/// Using it after the callback frame ends throws <see cref="ObjectDisposedException"/>.
/// </remarks>
public readonly ref struct LuauUserdataView
{
    private readonly LuauRefSource _source;

    internal LuauUserdataView(LuauState? state, int stackIndex, int callbackFrameToken)
    {
        _source = LuauRefSource.FromCallbackStack(state, stackIndex, callbackFrameToken, lua_Type.LUA_TUSERDATA);
    }

    /// <summary>
    /// Attempts to resolve this borrowed userdata as managed userdata of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Managed userdata type.</typeparam>
    /// <param name="value">Receives the managed userdata instance when successful.</param>
    /// <param name="error">Receives a descriptive error when resolution fails.</param>
    /// <returns>
    /// <c>true</c> when the userdata is managed userdata created by this library and matches
    /// <typeparamref name="T"/>; otherwise <c>false</c>.
    /// </returns>
    public bool TryGetManaged<T>([NotNullWhen(true)] out T? value, [NotNullWhen(false)] out string? error)
        where T : class, ILuauUserData<T> =>
        LuauUserdataAccessCore.TryGetManaged(_source, nameof(LuauUserdataView), out value, out error);

    /// <summary>
    /// Converts this borrowed userdata view to an <see cref="IntoLuau"/> value without creating an owned reference.
    /// </summary>
    /// <param name="value">The borrowed userdata view.</param>
    /// <returns>A temporary representation with the same callback-frame lifetime constraints.</returns>
    public static implicit operator IntoLuau(LuauUserdataView value) => IntoLuau.FromRefSource(value._source);

    /// <inheritdoc />
    public override string ToString() => _source.ToString();
}
