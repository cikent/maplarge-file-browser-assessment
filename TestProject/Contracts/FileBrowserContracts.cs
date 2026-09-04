namespace TestProject.Contracts;

public sealed record FileSystemEntryResponse(
    string Name,
    string Path,
    string Type,
    long? SizeBytes,
    DateTimeOffset ModifiedUtc);

public sealed record DirectorySummaryResponse(
    int FileCount,
    int FolderCount,
    long TotalFileBytes);

public sealed record BrowseResponse(
    string Path,
    string? ParentPath,
    IReadOnlyList<FileSystemEntryResponse> Entries,
    DirectorySummaryResponse Summary);

public sealed record SearchResponse(
    string Path,
    string Query,
    IReadOnlyList<FileSystemEntryResponse> Entries,
    DirectorySummaryResponse Summary,
    bool IsTruncated);

public sealed record CreateFolderRequest(string ParentPath, string Name);

public sealed record TransferEntryRequest(
    string SourcePath,
    string DestinationDirectory,
    string? NewName);
