using System.Diagnostics.CodeAnalysis;
using Darp.Luau.Internal.Require;

namespace Darp.Luau.Tests.Require;

internal sealed class FakeFileSystem : ILuauFileSystem
{
    private readonly string _currentDirectory;
    private readonly Dictionary<string, string> _files;
    private readonly HashSet<string> _directories;

    public FakeFileSystem((string FileName, string Content)[] files, string currentDirectory = "/scripts")
    {
        _currentDirectory = NormalizeInputPath(currentDirectory);
        (_files, _directories) = CreateFiles(files, currentDirectory);
    }

    public string GetCurrentDirectory() => _currentDirectory;

    public bool FileExists([NotNullWhen(true)] string? path) =>
        path is not null && _files.ContainsKey(ToAbsolutePath(path, _currentDirectory));

    public bool DirectoryExists([NotNullWhen(true)] string? path) =>
        path is not null && _directories.Contains(ToAbsolutePath(path, _currentDirectory));

    public string? ReadFile(string path) => _files.GetValueOrDefault(ToAbsolutePath(path, _currentDirectory));

    private static (Dictionary<string, string> Files, HashSet<string> Directories) CreateFiles(
        IEnumerable<(string FileName, string Content)> files,
        string currentDirectory
    )
    {
        var fileDict = new Dictionary<string, string>(StringComparer.Ordinal);
        var directorySet = new HashSet<string>(StringComparer.Ordinal);
        foreach ((string fileName, string content) in files)
        {
            if (!fileName.StartsWith("./", StringComparison.Ordinal) && !fileName.StartsWith('/'))
            {
                throw new ArgumentException(
                    $"Fake filesystem paths must be explicit relative paths starting with './' or absolute paths: {fileName}",
                    nameof(fileName)
                );
            }

            string absolutFileName = ToAbsolutePath(fileName, currentDirectory);
            AddParentDirectories(directorySet, absolutFileName);
            fileDict[absolutFileName] = content;
        }

        return (fileDict, directorySet);
    }

    private static void AddParentDirectories(HashSet<string> directories, string filePath)
    {
        string path = filePath;
        while (true)
        {
            int slashIndex = path.LastIndexOf('/');
            if (slashIndex < 0)
                return;

            path = slashIndex == 0 ? "/" : path[..slashIndex];
            if (!directories.Add(path) || path is "/" or "./")
                return;
        }
    }

    private static string NormalizeInputPath(string path) => path.Replace('\\', '/').TrimEnd('/');

    private static string ToAbsolutePath(string path, string currentDirectory) =>
        FileUtils.NormalizePath(FileUtils.IsAbsolutePath(path) ? path : $"{currentDirectory}/{path}");
}
