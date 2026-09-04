namespace TestProject.Tests;

internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"maplarge-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string CreateFile(string relativePath, string contents = "test content")
    {
        var fullPath = GetPath(relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, contents);
        return fullPath;
    }

    public string CreateDirectory(string relativePath)
    {
        var fullPath = GetPath(relativePath);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    public string GetPath(string relativePath) => System.IO.Path.Combine(
        Path,
        relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, true);
        }
    }
}
