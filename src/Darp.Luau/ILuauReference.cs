namespace Darp.Luau;

/// <summary> A Luau reference </summary>
internal interface ILuauReference : IDisposable
{
    /// <summary> The <see cref="LuauState"/> this reference is associated with </summary>
    LuauState? State { get; }
}
