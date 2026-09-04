using Microsoft.AspNetCore.Http.Features;
using TestProject.Middleware;
using TestProject.Options;
using TestProject.Services;

namespace TestProject;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services
            .AddOptions<FileBrowserOptions>()
            .Bind(builder.Configuration.GetSection(FileBrowserOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.RootPath),
                "FileBrowser:RootPath is required.")
            .Validate(options => options.SearchMaxResults is > 0 and <= 10_000,
                "FileBrowser:SearchMaxResults must be between 1 and 10,000.")
            .Validate(options => options.MaxUploadBytes is > 0
                    and <= FileBrowserOptions.MultipartBodyLengthLimit,
                "FileBrowser:MaxUploadBytes must be between 1 byte and 25 MiB.")
            .ValidateOnStart();

        builder.Services.Configure<FormOptions>(options =>
            options.MultipartBodyLengthLimit = FileBrowserOptions.MultipartBodyLengthLimit);
        builder.Services.AddSingleton<SafePathResolver>();
        builder.Services.AddSingleton<FileBrowserService>();
        builder.Services.AddControllers();

        var app = builder.Build();

        // Resolve at startup so a missing or unsafe home directory fails clearly.
        _ = app.Services.GetRequiredService<SafePathResolver>();

        app.UseMiddleware<FileBrowserExceptionMiddleware>();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapControllers();

        app.Run();
    }
}
