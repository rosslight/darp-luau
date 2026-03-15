namespace Darp.Luau;

/// <summary> A Luau reference </summary>
internal interface ILuauReference : IDisposable
{
    /// <summary> False, if this owned reference points to a valid lua ref; True, otherwise </summary>
    public bool IsDisposed { get; }

    /// <summary> Transfers ownership of this buffer reference into a <see cref="LuauValue"/>. </summary>
    /// <remarks>
    /// This method consumes the current owned reference.
    /// It does not clone ownership, so using this wrapper afterward is invalid.
    /// </remarks>
    /// <returns>The same underlying reference represented as a <see cref="LuauValue"/>.</returns>
    LuauValue DisposeAndToLuauValue();
}

/// <summary> A Luau reference </summary>
internal interface ILuauView<out TOwned>
    where TOwned : ILuauReference
{
    /// <summary> Creates an owned reference for this borrowed buffer. </summary>
    /// <returns> The newly created owned reference </returns>>
    TOwned ToOwned();
}
