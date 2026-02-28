using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Darp.Luau.Internal;
using Darp.Luau.Native;

namespace Darp.Luau;

internal struct LuauUserdataNative
{
    public const int Tag = 1;

    public GCHandle UserdataHandle { get; internal set; }
    public GCHandle RegistryValueHandle { get; internal set; }
}

public readonly struct LuauUserdata : IDisposable
{
    private readonly LuauRefSource _source;

    internal LuauState? State => _source.State;
    internal int Reference => _source.Reference;

    [Obsolete("Do not initialize the LuauTable. Create using the LuauState instead", true)]
    public LuauUserdata() { }

    internal LuauUserdata(LuauState? state, int reference)
    {
        _source = LuauRefSource.FromReference(state, reference, lua_Type.LUA_TUSERDATA);
    }

    /// <summary> Ability for <see cref="LuauUserdata"/> to be passed into functions that accept <see cref="IntoLuau"/> </summary>
    /// <param name="value"> The userdata </param>
    /// <returns> The converted value </returns>
    public static implicit operator IntoLuau(LuauUserdata value) => IntoLuau.FromRefSource(value._source);

    /// <summary>
    /// Attempts to resolve this userdata reference back to the managed userdata instance.
    /// </summary>
    /// <typeparam name="T">Managed userdata type.</typeparam>
    /// <param name="value">Receives the managed instance when successful.</param>
    /// <param name="error">Receives a descriptive error when resolution fails.</param>
    /// <returns>
    /// <c>true</c> when this reference points to managed userdata of type <typeparamref name="T"/>;
    /// otherwise <c>false</c>.
    /// </returns>
    public bool TryGetManaged<T>([NotNullWhen(true)] out T? value, [NotNullWhen(false)] out string? error)
        where T : class, ILuauUserData<T> =>
        LuauUserdataAccessCore.TryGetManaged(_source, nameof(LuauUserdata), out value, out error);

    /// <inheritdoc />
    public override string ToString() => _source.ToString();

    /// <inheritdoc />
    public void Dispose() => _source.Dispose();
}
