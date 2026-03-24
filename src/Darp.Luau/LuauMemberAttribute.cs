namespace Darp.Luau;

/// <summary> Marks a managed property or method as part of a generator-owned Luau surface. </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
public sealed class LuauMemberAttribute : Attribute
{
    /// <summary> Initializes a new instance of the <see cref="LuauMemberAttribute"/> class. </summary>
    /// <param name="name">The Luau-facing member name.</param>
    public LuauMemberAttribute(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
    }

    /// <summary> Gets the Luau-facing member name. </summary>
    public string Name { get; }

    /// <summary> Gets or sets the generated property access mode. </summary>
    public LuauPropertyAccess Access { get; init; } = LuauPropertyAccess.Auto;
}
