namespace Darp.Luau;

/// <summary> Controls the generated Luau read and write behavior for an exported property. </summary>
public enum LuauPropertyAccess
{
    /// <summary> Resolve access from the available getter and setter shape. </summary>
    Auto,

    /// <summary> Expose the property as read-only. </summary>
    ReadOnly,

    /// <summary> Expose the property as write-only. </summary>
    WriteOnly,

    /// <summary> Expose the property as read-write. </summary>
    ReadWrite,
}
