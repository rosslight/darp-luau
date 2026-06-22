namespace Darp.Luau;

/// <summary>Describes a host-provided module that can be loaded with <c>require(...)</c>.</summary>
/// <typeparam name="TSelf">Concrete module type.</typeparam>
public interface ILuauModule<TSelf>
    where TSelf : ILuauModule<TSelf>
{
    /// <summary>Gets the module name used by <c>require(...)</c>.</summary>
    static abstract string ModuleName { get; }

    /// <summary>Populates the module table when the module is first required.</summary>
    /// <param name="lua">The state loading the module.</param>
    /// <param name="table">The module table to populate.</param>
    void OnLoad(LuauState lua, in LuauTable table);
}
