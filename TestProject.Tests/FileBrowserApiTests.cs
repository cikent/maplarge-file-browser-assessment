using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace TestProject.Tests;

public sealed class FileBrowserApiTests
{
    [Fact]
    public async Task StaticApplicationRoot_ReturnsVanillaClient()
    {
        using var application = new TestApplication();
        using var client = application.CreateClient();

        var html = await client.GetStringAsync("/");

        Assert.Contains("Quality File Browser", html);
        Assert.Contains("scripts/app.js", html);
    }

    [Fact]
    public async Task Browse_AbsolutePathIsNeverDisclosedInJson()
    {
        using var application = new TestApplication();
        application.Root.CreateFile("reports/evidence.txt");
        using var client = application.CreateClient();

        var response = await client.GetAsync("/api/files/browse?path=reports");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(application.Root.Path, json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reports/evidence.txt", json);
    }

    [Fact]
    public async Task Browse_TraversalAttempt_ReturnsSafeProblemDetails()
    {
        using var application = new TestApplication();
        using var client = application.CreateClient();

        var response = await client.GetAsync("/api/files/browse?path=../outside");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Invalid path", problem.GetProperty("title").GetString());
        Assert.DoesNotContain(application.Root.Path, problem.ToString());
    }

    [Fact]
    public async Task Search_EmptyQuery_ReturnsBadRequest()
    {
        using var application = new TestApplication();
        using var client = application.CreateClient();

        var response = await client.GetAsync("/api/files/search?query=%20%20");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ApiWorkflow_FileAndFolderCrud_CompletesEndToEnd()
    {
        using var application = new TestApplication();
        using var client = application.CreateClient();

        var createFolder = await client.PostAsJsonAsync(
            "/api/files/folders",
            new { parentPath = "", name = "workspace" });
        Assert.Equal(HttpStatusCode.Created, createFolder.StatusCode);

        using var upload = new MultipartFormDataContent();
        upload.Add(new StringContent("workspace"), "path");
        upload.Add(new StringContent("false"), "overwrite");
        upload.Add(
            new ByteArrayContent(Encoding.UTF8.GetBytes("quality evidence")),
            "file",
            "evidence.txt");
        var uploadResponse = await client.PostAsync("/api/files/upload", upload);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);

        var copyResponse = await client.PostAsJsonAsync(
            "/api/files/copy",
            new
            {
                sourcePath = "workspace/evidence.txt",
                destinationDirectory = "workspace",
                newName = "evidence-copy.txt"
            });
        Assert.Equal(HttpStatusCode.OK, copyResponse.StatusCode);

        var moveResponse = await client.PostAsJsonAsync(
            "/api/files/move",
            new
            {
                sourcePath = "workspace/evidence-copy.txt",
                destinationDirectory = "",
                newName = "evidence-moved.txt"
            });
        Assert.Equal(HttpStatusCode.OK, moveResponse.StatusCode);

        var download = await client.GetStringAsync(
            "/api/files/download?path=evidence-moved.txt");
        Assert.Equal("quality evidence", download);

        var copyFolderResponse = await client.PostAsJsonAsync(
            "/api/files/copy",
            new
            {
                sourcePath = "workspace",
                destinationDirectory = "",
                newName = "workspace-copy"
            });
        Assert.Equal(HttpStatusCode.OK, copyFolderResponse.StatusCode);

        var deleteFile = await client.DeleteAsync(
            "/api/files?path=evidence-moved.txt&recursive=false");
        var deleteFolder = await client.DeleteAsync(
            "/api/files?path=workspace-copy&recursive=true");
        Assert.Equal(HttpStatusCode.NoContent, deleteFile.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleteFolder.StatusCode);
    }

    [Fact]
    public async Task Copy_SourceDeletedAfterSelection_ReturnsNotFoundAndNoPartialDestination()
    {
        using var application = new TestApplication();
        var physicalSource = application.Root.CreateFile("selected.txt");
        using var client = application.CreateClient();
        var browse = await client.GetAsync("/api/files/browse");
        browse.EnsureSuccessStatusCode();
        File.Delete(physicalSource);

        var response = await client.PostAsJsonAsync(
            "/api/files/copy",
            new
            {
                sourcePath = "selected.txt",
                destinationDirectory = "",
                newName = "destination.txt"
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(File.Exists(application.Root.GetPath("destination.txt")));
    }

    private sealed class TestApplication : WebApplicationFactory<Program>
    {
        public TestApplication()
        {
            Root = new TemporaryDirectory();
        }

        public TemporaryDirectory Root { get; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["FileBrowser:RootPath"] = Root.Path,
                    ["FileBrowser:SearchMaxResults"] = "25",
                    ["FileBrowser:MaxUploadBytes"] = "1000000"
                });
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                Root.Dispose();
            }
        }
    }
}
