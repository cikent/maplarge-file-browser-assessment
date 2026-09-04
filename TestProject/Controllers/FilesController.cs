using Microsoft.AspNetCore.Mvc;
using TestProject.Contracts;
using TestProject.Options;
using TestProject.Services;

namespace TestProject.Controllers;

[ApiController]
[Route("api/files")]
public sealed class FilesController : ControllerBase
{
    private readonly FileBrowserService _files;

    public FilesController(FileBrowserService files)
    {
        _files = files;
    }

    [HttpGet("browse")]
    public ActionResult<BrowseResponse> Browse([FromQuery] string? path = null)
    {
        return Ok(_files.Browse(path));
    }

    [HttpGet("search")]
    public ActionResult<SearchResponse> Search(
        [FromQuery] string query,
        [FromQuery] string? path,
        CancellationToken cancellationToken)
    {
        return Ok(_files.Search(path, query, cancellationToken));
    }

    [HttpGet("download")]
    public IActionResult Download([FromQuery] string path)
    {
        var download = _files.OpenDownload(path);
        return File(
            download.Stream,
            "application/octet-stream",
            download.FileName,
            enableRangeProcessing: true);
    }

    [HttpPost("upload")]
    [RequestSizeLimit(FileBrowserOptions.MultipartBodyLengthLimit)]
    public async Task<ActionResult<FileSystemEntryResponse>> Upload(
        [FromForm] IFormFile file,
        [FromForm] string? path,
        [FromForm] bool overwrite,
        CancellationToken cancellationToken)
    {
        if (file is null || string.IsNullOrWhiteSpace(file.FileName))
        {
            throw new FileBrowserException(
                StatusCodes.Status400BadRequest,
                "Invalid upload",
                "Select one named file to upload.");
        }

        await using var content = file.OpenReadStream();
        var created = await _files.UploadAsync(
            path,
            file.FileName,
            content,
            file.Length,
            overwrite,
            cancellationToken);
        return CreatedAtAction(nameof(Browse), new { path }, created);
    }

    [HttpPost("folders")]
    public ActionResult<FileSystemEntryResponse> CreateFolder(CreateFolderRequest request)
    {
        var created = _files.CreateFolder(request.ParentPath, request.Name);
        return CreatedAtAction(nameof(Browse), new { path = created.Path }, created);
    }

    [HttpPost("move")]
    public ActionResult<FileSystemEntryResponse> Move(TransferEntryRequest request)
    {
        return Ok(_files.Move(
            request.SourcePath,
            request.DestinationDirectory,
            request.NewName));
    }

    [HttpPost("copy")]
    public ActionResult<FileSystemEntryResponse> Copy(
        TransferEntryRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(_files.Copy(
            request.SourcePath,
            request.DestinationDirectory,
            request.NewName,
            cancellationToken));
    }

    [HttpDelete]
    public IActionResult Delete(
        [FromQuery] string path,
        [FromQuery] bool recursive = false)
    {
        _files.Delete(path, recursive);
        return NoContent();
    }
}
