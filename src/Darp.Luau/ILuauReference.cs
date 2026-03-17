namespace Darp.Luau;

/// <summary> A Luau value wrapper backed by a tracked registry reference. </summary>
internal interface ILuauReference : IDisposable
{
    /// <summary> Gets whether this wrapper no longer points to a tracked registry reference. </summary>
    public bool IsDisposed { get; }

    /// <summary> Rewraps the current tracked reference as a <see cref="LuauValue"/>. </summary>
    /// <remarks>
    /// The current wrapper is invalid after this call.
    /// The returned <see cref="LuauValue"/> keeps the same underlying registry reference alive until it is disposed.
    /// </remarks>
    /// <returns>The same underlying reference represented as a <see cref="LuauValue"/>.</returns>
    LuauValue DisposeAndToLuauValue();
}

/// <summary> A borrowed Luau view. </summary>
internal interface ILuauView<out TOwned>
    where TOwned : ILuauReference
{
    /// <summary> Creates a tracked owned reference for this borrowed value. </summary>
    /// <returns> The newly created owned reference </returns>>
    TOwned ToOwned();
}
