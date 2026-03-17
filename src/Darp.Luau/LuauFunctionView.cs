using Darp.Luau.Internal;
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
public readonly ref struct LuauFunctionView : ILuauView<LuauFunction>
{
    private readonly StackReference _reference;

    internal LuauFunctionView(LuauState state, int stackIndex) => _reference = new StackReference(state, stackIndex);

    /// <summary> Invokes the borrowed function with arguments and ignores any return values. </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the callback frame has ended or the state is disposed.</exception>
    /// <exception cref="LuaException">Thrown when Luau reports a call error.</exception>
    public void Invoke(params RefEnumerable<IntoLuau> args) => LuauFunctionInvokeCore.Invoke(_reference, args);

    /// <summary> Invokes the borrowed function with arguments and converts the first return value. </summary>
    /// <param name="args">The arguments passed to the Luau function.</param>
    /// <typeparam name="TR">Managed return type to convert to.</typeparam>
    /// <exception cref="ObjectDisposedException">Thrown when this reference is no longer tracked or the state is disposed.</exception>
    /// <exception cref="LuaException">Thrown when Luau reports a call error.</exception>
    /// <exception cref="InvalidCastException">
    /// Thrown when the first Luau return value cannot be converted to <typeparamref name="TR"/>.
    /// </exception>
    public TR Invoke<TR>(params RefEnumerable<IntoLuau> args)
    {
        return LuauFunctionInvokeCore.Invoke(_reference, args, static a => a.Read<TR>(1));
    }

    /// <summary> Invokes the borrowed function with arguments and converts the first two return values. </summary>
    /// <param name="args">The arguments passed to the Luau function.</param>
    /// <typeparam name="TR1">Managed return type to convert to.</typeparam>
    /// <typeparam name="TR2">Managed return type to convert to.</typeparam>
    /// <exception cref="ObjectDisposedException">Thrown when this reference is no longer tracked or the state is disposed.</exception>
    /// <exception cref="LuaException">Thrown when Luau reports a call error.</exception>
    /// <exception cref="InvalidCastException">
    /// Thrown when the Luau return values cannot be converted to <typeparamref name="TR1"/> or <typeparamref name="TR2"/>.
    /// </exception>
    public (TR1, TR2) Invoke<TR1, TR2>(params RefEnumerable<IntoLuau> args)
    {
        return LuauFunctionInvokeCore.Invoke(_reference, args, static a => (a.Read<TR1>(1), a.Read<TR2>(2)));
    }

    /// <summary> Invokes the borrowed function with arguments and converts the first three return values. </summary>
    /// <param name="args">The arguments passed to the Luau function.</param>
    /// <typeparam name="TR1">Managed return type to convert to.</typeparam>
    /// <typeparam name="TR2">Managed return type to convert to.</typeparam>
    /// <typeparam name="TR3">Managed return type to convert to.</typeparam>
    /// <exception cref="ObjectDisposedException">Thrown when this reference is no longer tracked or the state is disposed.</exception>
    /// <exception cref="LuaException">Thrown when Luau reports a call error.</exception>
    /// <exception cref="InvalidCastException">
    /// Thrown when the Luau return values cannot be converted to <typeparamref name="TR1"/>, <typeparamref name="TR2"/>, or <typeparamref name="TR3"/>.
    /// </exception>
    public (TR1, TR2, TR3) Invoke<TR1, TR2, TR3>(params RefEnumerable<IntoLuau> args)
    {
        return LuauFunctionInvokeCore.Invoke(
            _reference,
            args,
            static a => (a.Read<TR1>(1), a.Read<TR2>(2), a.Read<TR3>(3))
        );
    }

    /// <summary> Invokes the borrowed function with arguments and converts the first four return values. </summary>
    /// <param name="args">The arguments passed to the Luau function.</param>
    /// <typeparam name="TR1">Managed return type to convert to.</typeparam>
    /// <typeparam name="TR2">Managed return type to convert to.</typeparam>
    /// <typeparam name="TR3">Managed return type to convert to.</typeparam>
    /// <typeparam name="TR4">Managed return type to convert to.</typeparam>
    /// <exception cref="ObjectDisposedException">Thrown when this reference is no longer tracked or the state is disposed.</exception>
    /// <exception cref="LuaException">Thrown when Luau reports a call error.</exception>
    /// <exception cref="InvalidCastException">
    /// Thrown when the Luau return values cannot be converted to <typeparamref name="TR1"/>, <typeparamref name="TR2"/>, <typeparamref name="TR3"/>, or <typeparamref name="TR4"/>.
    /// </exception>
    public (TR1, TR2, TR3, TR4) Invoke<TR1, TR2, TR3, TR4>(params RefEnumerable<IntoLuau> args)
    {
        return LuauFunctionInvokeCore.Invoke(
            _reference,
            args,
            static a => (a.Read<TR1>(1), a.Read<TR2>(2), a.Read<TR3>(3), a.Read<TR4>(4))
        );
    }

    /// <summary> Invokes the borrowed function with arguments and returns all Luau return values as raw <see cref="LuauValue"/> instances. </summary>
    /// <param name="args">The arguments passed to the Luau function.</param>
    /// <exception cref="ObjectDisposedException">Thrown when this reference is no longer tracked or the state is disposed.</exception>
    /// <exception cref="LuaException">Thrown when Luau reports a call error.</exception>
    public LuauValue[] InvokeMulti(params RefEnumerable<IntoLuau> args)
    {
        return LuauFunctionInvokeCore.Invoke(
            _reference,
            args,
            static a =>
            {
                var values = new LuauValue[a.ArgumentCount];
                for (int i = 1; i <= values.Length; i++)
                {
                    if (!a.TryReadLuauValue(i, out LuauValue value, out string? error))
                        throw new ArgumentOutOfRangeException(nameof(args), error);
                    values[i - 1] = value;
                }
                return values;
            }
        );
    }

    /// <inheritdoc/>
    public LuauFunction ToOwned()
    {
        LuauState state = _reference.ValidateInternal();
        return new LuauFunction(state, ReferenceSourceExtensions.ToOwnedHandle(_reference));
    }

    /// <summary>
    /// Converts this borrowed function view to an <see cref="IntoLuau"/> value without creating an owned reference.
    /// </summary>
    /// <param name="value">The borrowed function view.</param>
    /// <returns>A temporary representation with the same callback-frame lifetime constraints.</returns>
    public static implicit operator IntoLuau(LuauFunctionView value) => IntoLuau.FromRefSource(value._reference);

    /// <inheritdoc />
    public override string ToString() => _reference.ToString();
}
