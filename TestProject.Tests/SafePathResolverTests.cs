using TestProject.Services;

namespace TestProject.Tests;

public sealed class SafePathResolverTests
{
    [Fact]
    public void ToRelativePath_Root_ReturnsEmptyPortablePath()
    {
        using var testRoot = new TemporaryDirectory();
        var resolver = new SafePathResolver(testRoot.Path);

        var relativePath = resolver.ToRelativePath(testRoot.Path);

        Assert.Equal(string.Empty, relativePath);
    }

    [Fact]
    public void ResolveExistingFile_Backslashes_NormalizesResponseToForwardSlashes()
    {
        using var testRoot = new TemporaryDirectory();
        var filePath = testRoot.CreateFile("reports/summary.txt");
        var resolver = new SafePathResolver(testRoot.Path);

        var resolvedPath = resolver.ResolveExistingFile("reports\\summary.txt");

        Assert.Equal(filePath, resolvedPath);
        Assert.Equal("reports/summary.txt", resolver.ToRelativePath(resolvedPath));
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("reports/../../outside.txt")]
    [InlineData("/absolute/path")]
    [InlineData("\\server\\share")]
    [InlineData("C:/Windows/System32")]
    [InlineData("reports/./summary.txt")]
    public void ResolveExistingEntry_UnsafePath_RejectsRequest(string unsafePath)
    {
        using var testRoot = new TemporaryDirectory();
        var resolver = new SafePathResolver(testRoot.Path);

        var exception = Assert.Throws<FileBrowserException>(
            () => resolver.ResolveExistingEntry(unsafePath));

        Assert.Equal(400, exception.StatusCode);
    }

    [Theory]
    [InlineData("nested/file.txt")]
    [InlineData("nested\\file.txt")]
    [InlineData("..")]
    [InlineData(" ")]
    public void ResolveDestination_NonLeafName_RejectsRequest(string invalidName)
    {
        using var testRoot = new TemporaryDirectory();
        var resolver = new SafePathResolver(testRoot.Path);

        var exception = Assert.Throws<FileBrowserException>(
            () => resolver.ResolveDestination(string.Empty, invalidName));

        Assert.Equal(400, exception.StatusCode);
    }

    [Fact]
    public void ResolveExistingEntry_SourceRemovedAfterSelection_ReturnsNotFound()
    {
        using var testRoot = new TemporaryDirectory();
        var filePath = testRoot.CreateFile("stale.txt");
        var resolver = new SafePathResolver(testRoot.Path);
        _ = resolver.ResolveExistingEntry("stale.txt");
        File.Delete(filePath);

        var exception = Assert.Throws<FileBrowserException>(
            () => resolver.ResolveExistingEntry("stale.txt"));

        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public void EnsureMutable_ConfiguredRoot_RejectsOperation()
    {
        using var testRoot = new TemporaryDirectory();
        var resolver = new SafePathResolver(testRoot.Path);

        var exception = Assert.Throws<FileBrowserException>(
            () => resolver.EnsureMutable(testRoot.Path));

        Assert.Equal(400, exception.StatusCode);
    }
}
