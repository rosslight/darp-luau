namespace Darp.Luau;

/// <summary> A Luau reference </summary>
public interface ILuauReference : IDisposable
{
    /// <summary> The <see cref="LuauState"/> this reference is associated with </summary>
    internal LuauState? State { get; }

    /// <summary> The reference </summary>
    internal int Reference { get; }
}
