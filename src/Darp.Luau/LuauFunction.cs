using Darp.Luau.Internal;
using Darp.Luau.Utils;

namespace Darp.Luau;

public readonly struct LuauFunction : IDisposable
{
    private readonly LuauState? _state;
    private readonly ulong _handle;

    /// <summary> True, if the <see cref="LuauTable"/> refers to a valid lua ref; False, otherwise </summary>
    public bool IsDisposed => !_state.IsReferenceValid(_handle);

    /// <summary> Do (not) initialize a new LuauFunction </summary>
    [Obsolete("Do not initialize the LuauFunction. Create using the LuauState instead", false)]
    public LuauFunction() { }

    internal LuauFunction(LuauState? state, ulong handle)
    {
        _state = state;
        _handle = handle;
    }

    /// <summary> Invokes the borrowed function with no arguments and converts the result. </summary>
    /// <typeparam name="TR">Managed return type to convert to. Use <see cref="LuauNil"/> for no return value.</typeparam>
    /// <returns>The converted return value.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the callback frame has ended or the state is disposed.</exception>
    /// <exception cref="LuaException">Thrown when Luau reports a call error.</exception>
    /// <exception cref="InvalidCastException">
    /// Thrown when the Luau return value cannot be converted to <typeparamref name="TR"/>.
    /// </exception>
    public TR Invoke<TR>()
        where TR : allows ref struct
    {
        return LuauFunctionInvokeCore.Invoke0<RegistryReferenceTracker.TrackedReference, TR>(
            _state.GetTrackedReferenceOrThrow(_handle)
        );
    }

    /// <summary> Invokes the borrowed function with one argument and converts the result. </summary>
    /// <typeparam name="TR">Managed return type to convert to. Use <see cref="LuauNil"/> for no return value.</typeparam>
    /// <param name="p1">First argument passed to the Luau function.</param>
    /// <returns>The converted return value.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the callback frame has ended or the state is disposed.</exception>
    /// <exception cref="LuaException">Thrown when Luau reports a call error.</exception>
    /// <exception cref="InvalidCastException">
    /// Thrown when the Luau return value cannot be converted to <typeparamref name="TR"/>.
    /// </exception>
    public TR Invoke<TR>(scoped in IntoLuau p1)
        where TR : allows ref struct
    {
        return LuauFunctionInvokeCore.Invoke1<RegistryReferenceTracker.TrackedReference, TR>(
            _state.GetTrackedReferenceOrThrow(_handle),
            p1
        );
    }

    /// <summary> Invokes the borrowed function with two arguments and converts the result. </summary>
    /// <typeparam name="TR">Managed return type to convert to. Use <see cref="LuauNil"/> for no return value.</typeparam>
    /// <param name="p1">First argument passed to the Luau function.</param>
    /// <param name="p2">Second argument passed to the Luau function.</param>
    /// <returns>The converted return value.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the callback frame has ended or the state is disposed.</exception>
    /// <exception cref="LuaException">Thrown when Luau reports a call error.</exception>
    /// <exception cref="InvalidCastException">
    /// Thrown when the Luau return value cannot be converted to <typeparamref name="TR"/>.
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

    /// <summary>
    /// Converts this function to an <see cref="IntoLuau"/> value without creating an owned reference.
    /// </summary>
    /// <param name="value">The borrowed function view.</param>
    /// <returns>A temporary representation with the same callback-frame lifetime constraints.</returns>
    public static implicit operator IntoLuau(LuauFunction value) => IntoLuau.Borrow(value._state, value._handle);

    /// <summary> Transfers ownership of this function reference into a <see cref="LuauValue"/>. </summary>
    /// <remarks>
    /// This method consumes the current <see cref="LuauFunction"/>.
    /// It does not clone ownership, so using this wrapper afterwards is invalid.
    /// </remarks>
    /// <returns>The same underlying reference represented as a <see cref="LuauValue"/>.</returns>
    public LuauValue DisposeAndToLuauValue() => LuauValue.Move(_state, _handle, LuauValueType.Function);

    /// <inheritdoc />
    public override string ToString() => Helpers.HandleToString(_state, _handle);

    /// <summary> Remove the reference from the lua state </summary>
    public void Dispose() => _state?.ReferenceTracker.ReleaseRef(_handle);
}
