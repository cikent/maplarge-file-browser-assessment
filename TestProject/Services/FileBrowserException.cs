namespace TestProject.Services;

/// <summary>Represents an expected filesystem failure safe to return to a client.</summary>
public sealed class FileBrowserException : Exception
{
    public FileBrowserException(int statusCode, string title, string detail)
        : base(detail)
    {
        StatusCode = statusCode;
        Title = title;
        Detail = detail;
    }

    public int StatusCode { get; }
    public string Title { get; }
    public string Detail { get; }
}
