namespace Darp.Luau.Utils;

/// <summary>public view on require context</summary>
public interface IRequireContext
{
    /// <summary>error if load of required module fails</summary>
    public string? LoadError { get; }
}
