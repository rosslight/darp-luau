namespace Darp.Luau;

/// <summary> Marks a managed type as a generator-owned Luau library surface. </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class LuauLibraryAttribute : Attribute
{
    /// <summary> Initializes a new instance of the <see cref="LuauLibraryAttribute"/> class. </summary>
    /// <param name="name">The Luau global name for the root library.</param>
    public LuauLibraryAttribute(string? name = null)
    {
        Name = name;
    }

    /// <summary> Gets the Luau global name for the root library. </summary>
    public string? Name { get; }
}
