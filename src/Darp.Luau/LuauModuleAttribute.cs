namespace Darp.Luau;

/// <summary> Marks a managed type as a generator-owned Luau module surface. </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class LuauModuleAttribute : Attribute
{
    /// <summary> Initializes a new instance of the <see cref="LuauModuleAttribute"/> class. </summary>
    /// <param name="name">The Luau require name for the module.</param>
    public LuauModuleAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary> Gets the Luau require name for the module. </summary>
    public string Name { get; }
}
