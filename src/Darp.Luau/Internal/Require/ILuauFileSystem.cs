using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;

namespace Darp.Luau.Internal.Require;

/// <summary> A virtual file system for abstract file operations. </summary>
public interface ILuauFileSystem
{
    /// <summary> Gets the current working directory. </summary>
    /// <returns> The current directory </returns>
    [Pure]
    string GetCurrentDirectory();

    /// <summary> Determines whether the given path refers to an existing file. </summary>
    /// <param name="path">The path to test.</param>
    /// <returns>True, if the file exists. False if the file does not exist or an error occured</returns>
    [Pure]
    bool FileExists([NotNullWhen(true)] string? path);

    /// <summary> Determines whether the given path refers to an existing directory. </summary>
    /// <param name="path">The path to test.</param>
    /// <returns>True, if the directory exists. False if the directory does not exist or an error occured</returns>
    [Pure]
    bool DirectoryExists([NotNullWhen(true)] string? path);

    /// <summary> Reads the content of a file. If the file does not exist, returns null. </summary>
    /// <param name="path"> The path to the file to read </param>
    /// <returns> The content of the file read or null </returns>
    [Pure]
    string? ReadFile(string path);
}

internal sealed class VirtualFileSystem : ILuauFileSystem
{
    public string GetCurrentDirectory() => Directory.GetCurrentDirectory();

    public bool FileExists([NotNullWhen(true)] string? path) => File.Exists(path);

    public bool DirectoryExists([NotNullWhen(true)] string? path) => Directory.Exists(path);

    public string? ReadFile(string path)
    {
        if (!FileExists(path))
            return null;
        return File.ReadAllText(path);
    }
}
