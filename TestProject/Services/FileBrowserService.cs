using Microsoft.Extensions.Options;
using TestProject.Contracts;
using TestProject.Options;

namespace TestProject.Services;

/// <summary>Owns bounded filesystem reads and mutations beneath one configured root.</summary>
public sealed class FileBrowserService
{
    private readonly FileBrowserOptions _options;
    private readonly SafePathResolver _paths;

    public FileBrowserService(
        SafePathResolver paths,
        IOptions<FileBrowserOptions> options)
    {
        _paths = paths;
        _options = options.Value;
    }

    public BrowseResponse Browse(string? relativePath)
    {
        var directoryPath = _paths.ResolveExistingDirectory(relativePath);
        var entries = EnumerateEntries(directoryPath)
            .OrderBy(entry => entry.Type == "folder" ? 0 : 1)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var currentPath = _paths.ToRelativePath(directoryPath);
        var parentDirectory = Directory.GetParent(directoryPath)?.FullName;
        var parentPath = parentDirectory is null
            || !_paths.IsSameOrDescendant(parentDirectory, _paths.RootPath)
            ? null
            : _paths.ToRelativePath(parentDirectory);

        return new BrowseResponse(currentPath, parentPath, entries, Summarize(entries));
    }

    public SearchResponse Search(
        string? relativePath,
        string? query,
        CancellationToken cancellationToken)
    {
        var searchRoot = _paths.ResolveExistingDirectory(relativePath);
        var normalizedQuery = query?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            throw new FileBrowserException(
                StatusCodes.Status400BadRequest,
                "Invalid search",
                "A non-empty search query is required.");
        }

        var matches = new List<FileSystemEntryResponse>();
        var isTruncated = false;
        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        foreach (var path in Directory.EnumerateFileSystemEntries(
            searchRoot,
            "*",
            enumerationOptions))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Path.GetFileName(path).Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (matches.Count == _options.SearchMaxResults)
            {
                isTruncated = true;
                break;
            }

            matches.Add(ToResponse(path));
        }

        var orderedMatches = matches
            .OrderBy(entry => entry.Type == "folder" ? 0 : 1)
            .ThenBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new SearchResponse(
            _paths.ToRelativePath(searchRoot),
            normalizedQuery,
            orderedMatches,
            Summarize(orderedMatches),
            isTruncated);
    }

    public async Task<FileSystemEntryResponse> UploadAsync(
        string? directoryPath,
        string fileName,
        Stream content,
        long length,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        if (length > _options.MaxUploadBytes)
        {
            throw new FileBrowserException(
                StatusCodes.Status413PayloadTooLarge,
                "Upload too large",
                $"Uploads are limited to {_options.MaxUploadBytes} bytes.");
        }

        var destinationPath = _paths.ResolveDestination(directoryPath, fileName);
        if (Directory.Exists(destinationPath)
            || (!overwrite && File.Exists(destinationPath)))
        {
            throw Conflict("An entry with that name already exists.");
        }

        var temporaryPath = Path.Combine(
            Path.GetDirectoryName(destinationPath)!,
            $".upload-{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var output = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81_920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await content.CopyToAsync(output, cancellationToken);
            }

            File.Move(temporaryPath, destinationPath, overwrite);
            return ToResponse(destinationPath);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    public FileSystemEntryResponse CreateFolder(string? parentPath, string name)
    {
        var destinationPath = _paths.ResolveDestination(parentPath, name);
        RejectExistingDestination(destinationPath);
        Directory.CreateDirectory(destinationPath);
        return ToResponse(destinationPath);
    }

    public FileSystemEntryResponse Move(
        string sourcePath,
        string destinationDirectory,
        string? newName)
    {
        var source = _paths.ResolveExistingEntry(sourcePath);
        _paths.EnsureMutable(source);
        var destination = _paths.ResolveDestination(
            destinationDirectory,
            string.IsNullOrWhiteSpace(newName) ? Path.GetFileName(source) : newName);
        RejectSelfOrDescendant(source, destination);
        RejectExistingDestination(destination);

        if (File.Exists(source))
        {
            File.Move(source, destination);
        }
        else
        {
            Directory.Move(source, destination);
        }

        return ToResponse(destination);
    }

    public FileSystemEntryResponse Copy(
        string sourcePath,
        string destinationDirectory,
        string? newName,
        CancellationToken cancellationToken)
    {
        var source = _paths.ResolveExistingEntry(sourcePath);
        _paths.EnsureMutable(source);
        var destination = _paths.ResolveDestination(
            destinationDirectory,
            string.IsNullOrWhiteSpace(newName) ? Path.GetFileName(source) : newName);
        RejectSelfOrDescendant(source, destination);
        RejectExistingDestination(destination);

        if (File.Exists(source))
        {
            CopyFileAtomically(source, destination, cancellationToken);
        }
        else
        {
            CopyDirectoryAtomically(source, destination, cancellationToken);
        }

        return ToResponse(destination);
    }

    public void Delete(string sourcePath, bool recursive)
    {
        var source = _paths.ResolveExistingEntry(sourcePath);
        _paths.EnsureMutable(source);

        if (File.Exists(source))
        {
            File.Delete(source);
            return;
        }

        Directory.Delete(source, recursive);
    }

    public (FileStream Stream, string FileName) OpenDownload(string relativePath)
    {
        var filePath = _paths.ResolveExistingFile(relativePath);
        var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81_920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return (stream, Path.GetFileName(filePath));
    }

    private IEnumerable<FileSystemEntryResponse> EnumerateEntries(string directoryPath)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(directoryPath))
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }

            yield return ToResponse(path);
        }
    }

    private FileSystemEntryResponse ToResponse(string path)
    {
        if (Directory.Exists(path))
        {
            var directory = new DirectoryInfo(path);
            return new FileSystemEntryResponse(
                directory.Name,
                _paths.ToRelativePath(path),
                "folder",
                null,
                directory.LastWriteTimeUtc);
        }

        var file = new FileInfo(path);
        if (!file.Exists)
        {
            throw new FileNotFoundException("The selected source no longer exists.");
        }

        return new FileSystemEntryResponse(
            file.Name,
            _paths.ToRelativePath(path),
            "file",
            file.Length,
            file.LastWriteTimeUtc);
    }

    private static DirectorySummaryResponse Summarize(
        IEnumerable<FileSystemEntryResponse> entries)
    {
        var materializedEntries = entries.ToArray();
        return new DirectorySummaryResponse(
            materializedEntries.Count(entry => entry.Type == "file"),
            materializedEntries.Count(entry => entry.Type == "folder"),
            materializedEntries.Sum(entry => entry.SizeBytes ?? 0));
    }

    private void RejectSelfOrDescendant(string source, string destination)
    {
        if (_paths.IsSameOrDescendant(destination, source))
        {
            throw new FileBrowserException(
                StatusCodes.Status400BadRequest,
                "Invalid destination",
                "An entry cannot be moved or copied onto itself or inside itself.");
        }
    }

    private static void RejectExistingDestination(string destination)
    {
        if (File.Exists(destination) || Directory.Exists(destination))
        {
            throw Conflict("An entry already exists at the destination.");
        }
    }

    private static void CopyFileAtomically(
        string source,
        string destination,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var temporaryPath = Path.Combine(
            Path.GetDirectoryName(destination)!,
            $".copy-{Guid.NewGuid():N}.tmp");

        try
        {
            File.Copy(source, temporaryPath);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, destination);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private static void CopyDirectoryAtomically(
        string source,
        string destination,
        CancellationToken cancellationToken)
    {
        var temporaryPath = Path.Combine(
            Path.GetDirectoryName(destination)!,
            $".copy-{Guid.NewGuid():N}.tmp");

        try
        {
            Directory.CreateDirectory(temporaryPath);
            CopyDirectoryContents(source, temporaryPath, cancellationToken);
            Directory.Move(temporaryPath, destination);
        }
        catch
        {
            if (Directory.Exists(temporaryPath))
            {
                Directory.Delete(temporaryPath, true);
            }

            throw;
        }
    }

    private static void CopyDirectoryContents(
        string source,
        string destination,
        CancellationToken cancellationToken)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(source))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
            {
                throw new FileBrowserException(
                    StatusCodes.Status400BadRequest,
                    "Linked path rejected",
                    "Folders containing symbolic links or reparse points cannot be copied.");
            }

            var target = Path.Combine(destination, Path.GetFileName(entry));
            if (File.Exists(entry))
            {
                File.Copy(entry, target);
            }
            else
            {
                Directory.CreateDirectory(target);
                CopyDirectoryContents(entry, target, cancellationToken);
            }
        }
    }

    private static FileBrowserException Conflict(string detail) => new(
        StatusCodes.Status409Conflict,
        "Filesystem conflict",
        detail);
}
