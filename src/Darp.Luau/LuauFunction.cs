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

    /// <summary> Invokes the referenced function and ignores any return values. </summary>
    /// <param name="args">The arguments passed to the Luau function.</param>
    /// <exception cref="ObjectDisposedException">Thrown when this reference is no longer tracked or the state is disposed.</exception>
    /// <exception cref="LuaException">Thrown when Luau reports a call error.</exception>
    public void Invoke(params RefEnumerable<IntoLuau> args) =>
        LuauFunctionInvokeCore.Invoke(_state.GetTrackedReferenceOrThrow(_handle), args);

    /// <summary> Invokes the referenced function and converts the return values. </summary>
    /// <param name="args">The arguments passed to the Luau function.</param>
    /// <typeparam name="TR">Managed return type to convert to.</typeparam>
    /// <exception cref="ObjectDisposedException">Thrown when this reference is no longer tracked or the state is disposed.</exception>
    /// <exception cref="LuaException">Thrown when Luau reports a call error.</exception>
    /// <exception cref="InvalidCastException">
    /// Thrown when the Luau return value cannot be converted to <typeparamref name="TR"/>.
    /// </exception>
    public TR Invoke<TR>(params RefEnumerable<IntoLuau> args)
    {
        return LuauFunctionInvokeCore.Invoke(
            _state.GetTrackedReferenceOrThrow(_handle),
            args,
            static a => a.Read<TR>(1)
        );
    }

    /// <summary> Invokes the referenced function and converts the return values. </summary>
    /// <param name="args">The arguments passed to the Luau function.</param>
    /// <typeparam name="TR1">Managed return type to convert to.</typeparam>
    /// <typeparam name="TR2">Managed return type to convert to.</typeparam>
    /// <exception cref="ObjectDisposedException">Thrown when this reference is no longer tracked or the state is disposed.</exception>
    /// <exception cref="LuaException">Thrown when Luau reports a call error.</exception>
    /// <exception cref="InvalidCastException">
    /// Thrown when the Luau return value cannot be converted to <typeparamref name="TR1"/> or <typeparamref name="TR2"/>.
    /// </exception>
    public (TR1, TR2) Invoke<TR1, TR2>(params RefEnumerable<IntoLuau> args)
    {
        return LuauFunctionInvokeCore.Invoke(
            _state.GetTrackedReferenceOrThrow(_handle),
            args,
            static a => (a.Read<TR1>(1), a.Read<TR2>(2))
        );
    }

    /// <summary> Invokes the referenced function and converts the return values. </summary>
    /// <param name="args">The arguments passed to the Luau function.</param>
    /// <typeparam name="TR1">Managed return type to convert to.</typeparam>
    /// <typeparam name="TR2">Managed return type to convert to.</typeparam>
    /// <typeparam name="TR3">Managed return type to convert to.</typeparam>
    /// <exception cref="ObjectDisposedException">Thrown when this reference is no longer tracked or the state is disposed.</exception>
    /// <exception cref="LuaException">Thrown when Luau reports a call error.</exception>
    /// <exception cref="InvalidCastException">
    /// Thrown when the Luau return value cannot be converted to <typeparamref name="TR1"/> or <typeparamref name="TR2"/> or <typeparamref name="TR3"/>.
    /// </exception>
    public (TR1, TR2, TR3) Invoke<TR1, TR2, TR3>(params RefEnumerable<IntoLuau> args)
    {
        return LuauFunctionInvokeCore.Invoke(
            _state.GetTrackedReferenceOrThrow(_handle),
            args,
            static a => (a.Read<TR1>(1), a.Read<TR2>(2), a.Read<TR3>(3))
        );
    }

    /// <summary> Invokes the referenced function and converts the return values. </summary>
    /// <param name="args">The arguments passed to the Luau function.</param>
    /// <exception cref="ObjectDisposedException">Thrown when this reference is no longer tracked or the state is disposed.</exception>
    /// <exception cref="LuaException">Thrown when Luau reports a call error.</exception>
    public LuauValue[] InvokeMulti(params RefEnumerable<IntoLuau> args)
    {
        return LuauFunctionInvokeCore.Invoke(
            _state.GetTrackedReferenceOrThrow(_handle),
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
