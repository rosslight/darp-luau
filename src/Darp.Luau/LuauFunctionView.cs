using Darp.Luau.Internal;
using Darp.Luau.Native;
using Darp.Luau.Utils;

namespace Darp.Luau;

/// <summary>
/// Represents a borrowed, stack-bound Luau function value read from callback arguments.
/// </summary>
/// <remarks>
/// This view does not own a registry reference.
/// It is valid only while the originating callback frame is active on the same <see cref="LuauState"/>.
/// Using it after the callback frame ends throws <see cref="ObjectDisposedException"/>.
/// </remarks>
public readonly ref struct LuauFunctionView
{
    private readonly StackReference _reference;

    internal LuauFunctionView(LuauState state, int stackIndex) => _reference = new StackReference(state, stackIndex);

    /// <summary> Invokes the borrowed function with no arguments and converts the result. </summary>
    /// <typeparam name="TR">Managed return type to convert to. Use <see cref="LuauNil"/> for no return value.</typeparam>
    /// <returns>The converted return value.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the callback frame has ended or the state is disposed.</exception>
    /// <exception cref="LuaException">Thrown when Luau reports a call error.</exception>
    /// <exception cref="InvalidCastException">
    /// Thrown when the Luau return value cannot be converted to <typeparamref name="TR"/>.
    /// </exception>
    public TR Invoke<TR>()
        where TR : allows ref struct => LuauFunctionInvokeCore.Invoke0<StackReference, TR>(_reference);

    /// <summary> Invokes the borrowed function with one argument and converts the result. </summary>
    /// <typeparam name="TR">Managed return type to convert to. Use <see cref="LuauNil"/> for no return value.</typeparam>
    /// <param name="p1">First argument passed to the Luau function.</param>
    /// <returns>The converted return value.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the callback frame has ended or the state is disposed.</exception>
    /// <exception cref="LuaException">Thrown when Luau reports a call error.</exception>
    /// <exception cref="InvalidCastException">
    /// Thrown when the Luau return value cannot be converted to <typeparamref name="TR"/>.
    /// </exception>
    public TR Invoke<TR>(in IntoLuau p1)
        where TR : allows ref struct => LuauFunctionInvokeCore.Invoke1<StackReference, TR>(_reference, p1);

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
    public TR Invoke<TR>(in IntoLuau p1, in IntoLuau p2)
        where TR : allows ref struct => LuauFunctionInvokeCore.Invoke2<StackReference, TR>(_reference, p1, p2);

    /// <summary>
    /// Converts this borrowed function view to an <see cref="IntoLuau"/> value without creating an owned reference.
    /// </summary>
    /// <param name="value">The borrowed function view.</param>
    /// <returns>A temporary representation with the same callback-frame lifetime constraints.</returns>
    public static implicit operator IntoLuau(LuauFunctionView value) => IntoLuau.FromRefSource(value._reference);

    /// <inheritdoc />
    public override string ToString() => _reference.ToString();
}
