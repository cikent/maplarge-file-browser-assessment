using System.Text;
using TestProject.Options;
using TestProject.Services;

namespace TestProject.Tests;

public sealed class FileBrowserServiceTests
{
    [Fact]
    public void Browse_MixedEntries_ReturnsSortedCountsAndImmediateFileSize()
    {
        using var testRoot = new TemporaryDirectory();
        testRoot.CreateDirectory("reports");
        testRoot.CreateFile("bravo.txt", "12345");
        testRoot.CreateFile("alpha.txt", "123");
        var service = CreateService(testRoot.Path);

        var response = service.Browse(string.Empty);

        Assert.Equal(2, response.Summary.FileCount);
        Assert.Equal(1, response.Summary.FolderCount);
        Assert.Equal(8, response.Summary.TotalFileBytes);
        Assert.Equal(new[] { "reports", "alpha.txt", "bravo.txt" },
            response.Entries.Select(entry => entry.Name));
    }

    [Fact]
    public void Search_NestedCaseInsensitiveMatch_ReturnsPortablePaths()
    {
        using var testRoot = new TemporaryDirectory();
        testRoot.CreateFile("reports/Quality-Summary.txt");
        testRoot.CreateFile("reports/unrelated.txt");
        var service = CreateService(testRoot.Path);

        var response = service.Search(string.Empty, "quality", CancellationToken.None);

        var match = Assert.Single(response.Entries);
        Assert.Equal("reports/Quality-Summary.txt", match.Path);
        Assert.False(response.IsTruncated);
    }

    [Fact]
    public void Search_ResultLimitExceeded_ReportsTruncation()
    {
        using var testRoot = new TemporaryDirectory();
        testRoot.CreateFile("match-one.txt");
        testRoot.CreateFile("match-two.txt");
        var service = CreateService(testRoot.Path, searchMaxResults: 1);

        var response = service.Search(string.Empty, "match", CancellationToken.None);

        Assert.Single(response.Entries);
        Assert.True(response.IsTruncated);
    }

    [Fact]
    public async Task FileCrud_CreateReadMoveCopyDelete_PreservesExpectedContents()
    {
        using var testRoot = new TemporaryDirectory();
        testRoot.CreateDirectory("workspace");
        var service = CreateService(testRoot.Path);
        await using var upload = new MemoryStream(Encoding.UTF8.GetBytes("quality evidence"));

        var created = await service.UploadAsync(
            "workspace", "evidence.txt", upload, upload.Length, false, CancellationToken.None);
        var copied = service.Copy(
            created.Path, "workspace", "evidence-copy.txt", CancellationToken.None);
        var moved = service.Move(copied.Path, string.Empty, "evidence-moved.txt");
        var download = service.OpenDownload(moved.Path);
        string contents;
        await using (var downloadStream = download.Stream)
        using (var reader = new StreamReader(downloadStream))
        {
            contents = await reader.ReadToEndAsync();
        }

        service.Delete(moved.Path, false);

        Assert.Equal("quality evidence", contents);
        Assert.True(File.Exists(testRoot.GetPath("workspace/evidence.txt")));
        Assert.False(File.Exists(testRoot.GetPath("evidence-moved.txt")));
    }

    [Fact]
    public void FolderCrud_CreateCopyMoveDelete_PreservesNestedFiles()
    {
        using var testRoot = new TemporaryDirectory();
        var service = CreateService(testRoot.Path);
        var created = service.CreateFolder(string.Empty, "source");
        testRoot.CreateFile("source/nested/evidence.txt", "nested quality evidence");

        var copied = service.Copy(
            created.Path, string.Empty, "copy", CancellationToken.None);
        var moved = service.Move(copied.Path, string.Empty, "renamed");
        service.Delete(created.Path, true);

        Assert.Equal("renamed", moved.Path);
        Assert.True(File.Exists(testRoot.GetPath("renamed/nested/evidence.txt")));
        Assert.False(Directory.Exists(testRoot.GetPath("source")));
    }

    [Fact]
    public void Copy_SourceDeletedAfterBrowse_ReturnsNotFoundWithoutDestination()
    {
        using var testRoot = new TemporaryDirectory();
        var sourcePath = testRoot.CreateFile("selected.txt");
        var service = CreateService(testRoot.Path);
        _ = service.Browse(string.Empty);
        File.Delete(sourcePath);

        var exception = Assert.Throws<FileBrowserException>(() => service.Copy(
            "selected.txt", string.Empty, "destination.txt", CancellationToken.None));

        Assert.Equal(404, exception.StatusCode);
        Assert.False(File.Exists(testRoot.GetPath("destination.txt")));
    }

    [Fact]
    public void CopyFolder_CanceledOperation_LeavesNoDestinationOrStagingDirectory()
    {
        using var testRoot = new TemporaryDirectory();
        testRoot.CreateFile("source/large.txt", new string('x', 10_000));
        var service = CreateService(testRoot.Path);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => service.Copy(
            "source", string.Empty, "destination", cancellation.Token));

        Assert.False(Directory.Exists(testRoot.GetPath("destination")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(testRoot.Path, ".copy-*.tmp"));
    }

    [Fact]
    public void MoveFolder_IntoOwnDescendant_RejectsWithoutMutation()
    {
        using var testRoot = new TemporaryDirectory();
        testRoot.CreateDirectory("source/child");
        var service = CreateService(testRoot.Path);

        var exception = Assert.Throws<FileBrowserException>(() =>
            service.Move("source", "source/child", "moved"));

        Assert.Equal(400, exception.StatusCode);
        Assert.True(Directory.Exists(testRoot.GetPath("source/child")));
    }

    [Fact]
    public async Task Upload_DuplicateWithoutOverwrite_RejectsExistingFile()
    {
        using var testRoot = new TemporaryDirectory();
        testRoot.CreateFile("evidence.txt", "original");
        var service = CreateService(testRoot.Path);
        await using var upload = new MemoryStream(Encoding.UTF8.GetBytes("replacement"));

        var exception = await Assert.ThrowsAsync<FileBrowserException>(() =>
            service.UploadAsync(
                string.Empty,
                "evidence.txt",
                upload,
                upload.Length,
                false,
                CancellationToken.None));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal("original", File.ReadAllText(testRoot.GetPath("evidence.txt")));
    }

    [Fact]
    public async Task Upload_DuplicateWithExplicitOverwrite_ReplacesContents()
    {
        using var testRoot = new TemporaryDirectory();
        testRoot.CreateFile("evidence.txt", "original");
        var service = CreateService(testRoot.Path);
        await using var upload = new MemoryStream(Encoding.UTF8.GetBytes("replacement"));

        await service.UploadAsync(
            string.Empty,
            "evidence.txt",
            upload,
            upload.Length,
            true,
            CancellationToken.None);

        Assert.Equal("replacement", File.ReadAllText(testRoot.GetPath("evidence.txt")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(testRoot.Path, ".upload-*.tmp"));
    }

    [Fact]
    public async Task Upload_AboveConfiguredLimit_RejectsWithoutCreatingFile()
    {
        using var testRoot = new TemporaryDirectory();
        var service = CreateService(testRoot.Path, maxUploadBytes: 4);
        await using var upload = new MemoryStream(Encoding.UTF8.GetBytes("12345"));

        var exception = await Assert.ThrowsAsync<FileBrowserException>(() =>
            service.UploadAsync(
                string.Empty,
                "oversize.txt",
                upload,
                upload.Length,
                false,
                CancellationToken.None));

        Assert.Equal(413, exception.StatusCode);
        Assert.False(File.Exists(testRoot.GetPath("oversize.txt")));
    }

    [Fact]
    public void Copy_ExistingDestination_RejectsWithoutOverwriting()
    {
        using var testRoot = new TemporaryDirectory();
        testRoot.CreateFile("source.txt", "source");
        testRoot.CreateFile("destination.txt", "destination");
        var service = CreateService(testRoot.Path);

        var exception = Assert.Throws<FileBrowserException>(() => service.Copy(
            "source.txt", string.Empty, "destination.txt", CancellationToken.None));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal("destination", File.ReadAllText(testRoot.GetPath("destination.txt")));
    }

    [Fact]
    public void Delete_MissingSource_ReturnsNotFound()
    {
        using var testRoot = new TemporaryDirectory();
        var service = CreateService(testRoot.Path);

        var exception = Assert.Throws<FileBrowserException>(() =>
            service.Delete("missing.txt", false));

        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public void Delete_ConfiguredRoot_RejectsWithoutDataLoss()
    {
        using var testRoot = new TemporaryDirectory();
        testRoot.CreateFile("evidence.txt");
        var service = CreateService(testRoot.Path);

        var exception = Assert.Throws<FileBrowserException>(() =>
            service.Delete(string.Empty, true));

        Assert.Equal(400, exception.StatusCode);
        Assert.True(File.Exists(testRoot.GetPath("evidence.txt")));
    }

    [Fact]
    public void Delete_NonEmptyFolderWithoutRecursiveFlag_RejectsWithoutDataLoss()
    {
        using var testRoot = new TemporaryDirectory();
        testRoot.CreateFile("folder/evidence.txt");
        var service = CreateService(testRoot.Path);

        Assert.Throws<IOException>(() => service.Delete("folder", false));

        Assert.True(File.Exists(testRoot.GetPath("folder/evidence.txt")));
    }

    private static FileBrowserService CreateService(
        string rootPath,
        int searchMaxResults = 200,
        long maxUploadBytes = 1_000_000)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new FileBrowserOptions
        {
            RootPath = rootPath,
            SearchMaxResults = searchMaxResults,
            MaxUploadBytes = maxUploadBytes
        });
        return new FileBrowserService(new SafePathResolver(rootPath), options);
    }
}
