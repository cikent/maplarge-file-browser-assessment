namespace TestProject.Options;

/// <summary>Controls the filesystem boundary and bounded operations.</summary>
public sealed class FileBrowserOptions
{
    public const string SectionName = "FileBrowser";
    public const long MultipartBodyLengthLimit = 26_214_400;

    public string RootPath { get; init; } = "SampleFiles";
    public int SearchMaxResults { get; init; } = 200;
    public long MaxUploadBytes { get; init; } = 26_214_400;
}
