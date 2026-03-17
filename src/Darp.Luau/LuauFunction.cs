using Darp.Luau.Internal;
using Darp.Luau.Utils;

namespace Darp.Luau;

public readonly struct LuauFunction : ILuauReference
{
    private readonly LuauState? _state;
    private readonly ulong _handle;

    /// <summary>
    /// Gets whether this wrapper no longer points to a tracked Luau function reference.
    /// </summary>
    public bool IsDisposed => !_state.IsReferenceValid(_handle);

    /// <summary> Do (not) initialize a new LuauFunction </summary>
    [Obsolete("Do not initialize the LuauFunction. Create using the LuauState instead", true)]
    public LuauFunction() { }

    internal LuauFunction(LuauState? state, ulong handle)
    {
        _state = state;
        _handle = handle;
    }

    /// <summary> Invokes the referenced function with no arguments and converts the first return value. </summary>
    /// <typeparam name="TR">Managed return type to convert to. Use <see cref="LuauNil"/> for no return value.</typeparam>
    /// <returns>The converted return value.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this reference is no longer tracked or the state is disposed.</exception>
    /// <exception cref="LuaException">Thrown when Luau reports a call error.</exception>
    /// <exception cref="InvalidCastException">
    /// Thrown when the first Luau return value cannot be converted to <typeparamref name="TR"/>.
    /// </exception>
    public TR Invoke<TR>()
        where TR : allows ref struct
    {
        return LuauFunctionInvokeCore.Invoke0<RegistryReferenceTracker.TrackedReference, TR>(
            _state.GetTrackedReferenceOrThrow(_handle)
        );
    }

    /// <summary> Invokes the referenced function with no arguments and ignores any return values. </summary>
    /// <returns>The converted return value.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this reference is no longer tracked or the state is disposed.</exception>
    /// <exception cref="LuaException">Thrown when Luau reports a call error.</exception>
    public void Invoke() => Invoke<LuauNil>();

    /// <summary> Invokes the referenced function with one argument and converts the first return value. </summary>
    /// <typeparam name="TR">Managed return type to convert to. Use <see cref="LuauNil"/> for no return value.</typeparam>
    /// <param name="p1">First argument passed to the Luau function.</param>
    /// <returns>The converted return value.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this reference is no longer tracked or the state is disposed.</exception>
    /// <exception cref="LuaException">Thrown when Luau reports a call error.</exception>
    /// <exception cref="InvalidCastException">
    /// Thrown when the first Luau return value cannot be converted to <typeparamref name="TR"/>.
    /// </exception>
    public TR Invoke<TR>(scoped in IntoLuau p1)
        where TR : allows ref struct
    {
        return LuauFunctionInvokeCore.Invoke1<RegistryReferenceTracker.TrackedReference, TR>(
            _state.GetTrackedReferenceOrThrow(_handle),
            p1
        );
    }

    /// <summary> Invokes the referenced function with one argument and ignores any return values. </summary>
    /// <param name="p1">First argument passed to the Luau function.</param>
    /// <returns>The converted return value.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this reference is no longer tracked or the state is disposed.</exception>
    /// <exception cref="LuaException">Thrown when Luau reports a call error.</exception>
    public void Invoke(in IntoLuau p1) => Invoke<LuauNil>(p1);

    /// <summary> Invokes the referenced function with two arguments and converts the first return value. </summary>
    /// <typeparam name="TR">Managed return type to convert to. Use <see cref="LuauNil"/> for no return value.</typeparam>
    /// <param name="p1">First argument passed to the Luau function.</param>
    /// <param name="p2">Second argument passed to the Luau function.</param>
    /// <returns>The converted return value.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this reference is no longer tracked or the state is disposed.</exception>
    /// <exception cref="LuaException">Thrown when Luau reports a call error.</exception>
    /// <exception cref="InvalidCastException">
    /// Thrown when the first Luau return value cannot be converted to <typeparamref name="TR"/>.
    /// </exception>
    public TR Invoke<TR>(scoped in IntoLuau p1, scoped in IntoLuau p2)
        where TR : allows ref struct
    {
        return LuauFunctionInvokeCore.Invoke2<RegistryReferenceTracker.TrackedReference, TR>(
            _state.GetTrackedReferenceOrThrow(_handle),
            p1,
            p2
        );
    }

    /// <summary> Invokes the referenced function with two arguments and ignores any return values. </summary>
    /// <param name="p1">First argument passed to the Luau function.</param>
    /// <param name="p2">Second argument passed to the Luau function.</param>
    /// <returns>The converted return value.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this reference is no longer tracked or the state is disposed.</exception>
    /// <exception cref="LuaException">Thrown when Luau reports a call error.</exception>
    public void Invoke(in IntoLuau p1, in IntoLuau p2) => Invoke<LuauNil>(p1, p2);

    /// <summary> Invokes the referenced function with three arguments and converts the first return value. </summary>
    /// <typeparam name="TR">Managed return type to convert to. Use <see cref="LuauNil"/> for no return value.</typeparam>
    /// <param name="p1">First argument passed to the Luau function.</param>
    /// <param name="p2">Second argument passed to the Luau function.</param>
    /// <param name="p3">Third argument passed to the Luau function.</param>
    /// <returns>The converted return value.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this reference is no longer tracked or the state is disposed.</exception>
    /// <exception cref="LuaException">Thrown when Luau reports a call error.</exception>
    /// <exception cref="InvalidCastException">
    /// Thrown when the first Luau return value cannot be converted to <typeparamref name="TR"/>.
    /// </exception>
    public TR Invoke<TR>(scoped in IntoLuau p1, scoped in IntoLuau p2, scoped in IntoLuau p3)
        where TR : allows ref struct
    {
        return LuauFunctionInvokeCore.Invoke3<RegistryReferenceTracker.TrackedReference, TR>(
            _state.GetTrackedReferenceOrThrow(_handle),
            p1,
            p2,
            p3
        );
    }

    /// <summary> Invokes the referenced function with three arguments and ignores any return values. </summary>
    /// <param name="p1">First argument passed to the Luau function.</param>
    /// <param name="p2">Second argument passed to the Luau function.</param>
    /// <param name="p3">Third argument passed to the Luau function.</param>
    /// <returns>The converted return value.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this reference is no longer tracked or the state is disposed.</exception>
    /// <exception cref="LuaException">Thrown when Luau reports a call error.</exception>
    public void Invoke(in IntoLuau p1, in IntoLuau p2, in IntoLuau p3) => Invoke<LuauNil>(p1, p2, p3);

    /// <summary>
    /// Converts this function to an <see cref="IntoLuau"/> value without creating another tracked reference.
    /// </summary>
    /// <param name="value">The tracked function reference.</param>
    /// <returns>A temporary representation of the same tracked function.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this reference is no longer tracked.</exception>
    public static implicit operator IntoLuau(LuauFunction value) => IntoLuau.Borrow(value._state, value._handle);

    /// <inheritdoc/>
    public LuauValue DisposeAndToLuauValue() => LuauValue.Move(_state, _handle, LuauValueType.Function);

    /// <inheritdoc />
    public override string ToString() => Helpers.HandleToString(_state, _handle);

    /// <summary> Remove the reference from the lua state </summary>
    public void Dispose() => _state?.ReferenceTracker.ReleaseRef(_handle);
}
