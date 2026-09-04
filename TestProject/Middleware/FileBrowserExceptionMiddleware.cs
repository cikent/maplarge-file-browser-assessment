using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using TestProject.Services;

namespace TestProject.Middleware;

/// <summary>Maps expected filesystem failures without exposing server paths.</summary>
public sealed class FileBrowserExceptionMiddleware
{
    private static readonly JsonSerializerOptions ProblemJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RequestDelegate _next;
    private readonly ILogger<FileBrowserExceptionMiddleware> _logger;

    public FileBrowserExceptionMiddleware(
        RequestDelegate next,
        ILogger<FileBrowserExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation("The client canceled filesystem request {TraceId}.", context.TraceIdentifier);
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            await WriteProblemAsync(context, exception);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception exception)
    {
        var problem = exception switch
        {
            FileBrowserException expected => new ProblemDetails
            {
                Status = expected.StatusCode,
                Title = expected.Title,
                Detail = expected.Detail
            },
            FileNotFoundException or DirectoryNotFoundException => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Entry not found",
                Detail = "The selected source changed or no longer exists. Refresh and try again."
            },
            UnauthorizedAccessException => new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Access denied",
                Detail = "The server cannot access the selected entry."
            },
            IOException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Filesystem conflict",
                Detail = "The filesystem changed during the operation. Refresh and try again."
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Unexpected error",
                Detail = "The request could not be completed."
            }
        };

        problem.Extensions["traceId"] = context.TraceIdentifier;
        if (problem.Status >= 500)
        {
            _logger.LogError(exception, "Unhandled filesystem request failure {TraceId}.", context.TraceIdentifier);
        }
        else
        {
            _logger.LogWarning(exception, "Filesystem request rejected {TraceId}.", context.TraceIdentifier);
        }

        context.Response.StatusCode = problem.Status!.Value;
        await context.Response.WriteAsJsonAsync(
            problem,
            ProblemJsonOptions,
            "application/problem+json");
    }
}
