namespace Darp.Luau;

/// <summary> Marks a managed class as a generator-owned Luau userdata surface. </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class LuauUserdataAttribute : Attribute;
