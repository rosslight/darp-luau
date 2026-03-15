using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Darp.Luau.Internal;
using Darp.Luau.Native;
using Darp.Luau.Utils;

namespace Darp.Luau;

internal struct LuauUserdataNative
{
    public const int Tag = 1;

    public GCHandle UserdataHandle { get; internal set; }
    public GCHandle RegistryValueHandle { get; internal set; }
}

public readonly struct LuauUserdata : IDisposable, IEquatable<LuauUserdata>
{
    private readonly LuauState? _state;
    private readonly ulong _handle;

    /// <summary> True, if the <see cref="LuauTable"/> refers to a valid lua ref; False, otherwise </summary>
    public bool IsDisposed => !_state.IsReferenceValid(_handle);

    [Obsolete("Do not initialize the LuauTable. Create using the LuauState instead", true)]
    public LuauUserdata() { }

    internal LuauUserdata(LuauState state, ulong handle)
    {
        _state = state;
        _handle = handle;
    }

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
        where T : class, ILuauUserData<T>
    {
        value = null;
        error = null;
        return _state.TryGetTrackedReference(_handle, out RegistryReferenceTracker.TrackedReference? reference)
            && LuauUserdataAccessCore.TryGetManaged(reference, out value, out error);
    }

    /// <summary> Ability for <see cref="LuauUserdata"/> to be passed into functions that accept <see cref="IntoLuau"/> </summary>
    /// <param name="value"> The userdata </param>
    /// <returns> The converted value </returns>
    public static implicit operator IntoLuau(LuauUserdata value) => IntoLuau.Borrow(value._state, value._handle);

    /// <summary> Converts this string reference into a <see cref="LuauValue"/>. </summary>
    /// <remarks> Calling this method releases the reference of the current <see cref="LuauUserdata"/> </remarks>
    /// <returns> The reference as a luauValue </returns>
    /// <summary> Transfers ownership of this userdata reference into a <see cref="LuauValue"/>. </summary>
    /// <remarks>
    /// This method consumes the current <see cref="LuauUserdata"/>.
    /// It does not clone ownership, so using this wrapper afterwards is invalid.
    /// </remarks>
    /// <returns>The same underlying reference represented as a <see cref="LuauValue"/>.</returns>
    public LuauValue DisposeAndToLuauValue() => LuauValue.Move(_state, _handle, LuauValueType.Userdata);

    /// <inheritdoc />
    public bool Equals(LuauUserdata other) => other._state == _state && other._handle == _handle;

    /// <inheritdoc />
    public override string ToString() => Helpers.HandleToString(_state, _handle);

    /// <inheritdoc />
    public void Dispose() => _state?.ReferenceTracker.ReleaseRef(_handle);
}
