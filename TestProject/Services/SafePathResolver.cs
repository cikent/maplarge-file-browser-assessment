using Microsoft.Extensions.Options;
using TestProject.Options;

namespace TestProject.Services;

/// <summary>
/// Converts portable API-relative paths to native paths while enforcing the
/// configured root as an invariant for every filesystem operation.
/// </summary>
public sealed class SafePathResolver
{
    private readonly StringComparison _pathComparison;
    private readonly string _rootWithSeparator;

    public SafePathResolver(
        IOptions<FileBrowserOptions> options,
        IWebHostEnvironment environment)
        : this(GetConfiguredRoot(options.Value.RootPath, environment.ContentRootPath))
    {
    }

    public SafePathResolver(string rootPath)
    {
        RootPath = Path.GetFullPath(rootPath);
        _pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        _rootWithSeparator = WithTrailingSeparator(RootPath);

        if (!Directory.Exists(RootPath))
        {
            throw new InvalidOperationException(
                "The configured FileBrowser root directory does not exist.");
        }

        RejectReparsePoint(RootPath);
    }

    public string RootPath { get; }

    public string ResolveExistingDirectory(string? relativePath)
    {
        var fullPath = Resolve(relativePath);
        if (!Directory.Exists(fullPath))
        {
            throw NotFound("The requested folder no longer exists.");
        }

        RejectExistingReparsePoints(fullPath);
        return fullPath;
    }

    public string ResolveExistingFile(string? relativePath)
    {
        var fullPath = Resolve(relativePath);
        if (!File.Exists(fullPath))
        {
            throw NotFound("The requested file no longer exists.");
        }

        RejectExistingReparsePoints(fullPath);
        return fullPath;
    }

    public string ResolveExistingEntry(string? relativePath)
    {
        var fullPath = Resolve(relativePath);
        if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
        {
            throw NotFound("The selected source no longer exists.");
        }

        RejectExistingReparsePoints(fullPath);
        return fullPath;
    }

    public string ResolveDestination(string? directoryPath, string name)
    {
        ValidateName(name);
        var parentPath = ResolveExistingDirectory(directoryPath);
        var destinationPath = Path.GetFullPath(Path.Combine(parentPath, name));
        EnsureContained(destinationPath);
        return destinationPath;
    }

    public void EnsureMutable(string fullPath)
    {
        if (string.Equals(
            Path.TrimEndingDirectorySeparator(fullPath),
            Path.TrimEndingDirectorySeparator(RootPath),
            _pathComparison))
        {
            throw new FileBrowserException(
                StatusCodes.Status400BadRequest,
                "Root operation rejected",
                "The configured root folder cannot be moved, copied, renamed, or deleted.");
        }
    }

    public string ToRelativePath(string fullPath)
    {
        EnsureContained(fullPath);
        var relativePath = Path.GetRelativePath(RootPath, fullPath);
        return relativePath == "."
            ? string.Empty
            : relativePath.Replace(Path.DirectorySeparatorChar, '/');
    }

    public bool IsSameOrDescendant(string candidatePath, string directoryPath)
    {
        var candidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidatePath));
        var directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directoryPath));
        return string.Equals(candidate, directory, _pathComparison)
            || candidate.StartsWith(WithTrailingSeparator(directory), _pathComparison);
    }

    private static string GetConfiguredRoot(string configuredRoot, string contentRoot)
    {
        return Path.IsPathRooted(configuredRoot)
            ? configuredRoot
            : Path.Combine(contentRoot, configuredRoot);
    }

    private string Resolve(string? relativePath)
    {
        var requestedPath = relativePath ?? string.Empty;
        if (requestedPath.StartsWith('/')
            || requestedPath.StartsWith('\\')
            || Path.IsPathRooted(requestedPath)
            || IsDriveQualified(requestedPath))
        {
            throw InvalidPath("Absolute and root-relative paths are not allowed.");
        }

        var normalizedPath = requestedPath.Replace('\\', '/');
        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment is "." or ".."))
        {
            throw InvalidPath("Relative traversal segments are not allowed.");
        }

        if (normalizedPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            throw InvalidPath("The path contains invalid characters.");
        }

        var nativePath = string.Join(Path.DirectorySeparatorChar, segments);
        var fullPath = Path.GetFullPath(Path.Combine(RootPath, nativePath));
        EnsureContained(fullPath);
        return fullPath;
    }

    private void EnsureContained(string fullPath)
    {
        var canonicalPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(fullPath));
        var canonicalRoot = Path.TrimEndingDirectorySeparator(RootPath);
        if (!string.Equals(canonicalPath, canonicalRoot, _pathComparison)
            && !canonicalPath.StartsWith(_rootWithSeparator, _pathComparison))
        {
            throw InvalidPath("The path must remain inside the configured root.");
        }
    }

    private void RejectExistingReparsePoints(string fullPath)
    {
        var relativePath = Path.GetRelativePath(RootPath, fullPath);
        var currentPath = RootPath;
        foreach (var segment in relativePath.Split(
            Path.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);
            if (!File.Exists(currentPath) && !Directory.Exists(currentPath))
            {
                break;
            }

            RejectReparsePoint(currentPath);
        }
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new FileBrowserException(
                StatusCodes.Status400BadRequest,
                "Linked path rejected",
                "Symbolic links and filesystem reparse points are outside this proof-of-concept boundary.");
        }
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)
            || name is "." or ".."
            || name.Contains('/')
            || name.Contains('\\')
            || name != Path.GetFileName(name)
            || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw InvalidPath("A single valid file or folder name is required.");
        }
    }

    private static bool IsDriveQualified(string path) =>
        path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':';

    private static string WithTrailingSeparator(string path)
    {
        return Path.EndsInDirectorySeparator(path)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static FileBrowserException InvalidPath(string detail) => new(
        StatusCodes.Status400BadRequest,
        "Invalid path",
        detail);

    private static FileBrowserException NotFound(string detail) => new(
        StatusCodes.Status404NotFound,
        "Entry not found",
        detail);
}
