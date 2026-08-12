using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AutoParts.Api.Infrastructure;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var status = exception is ApiException api ? api.StatusCode : StatusCodes.Status500InternalServerError;
        if (status >= 500) logger.LogError(exception, "Unhandled API error");
        var problem = new ProblemDetails
        {
            Status = status,
            Title = status == 500 ? "An unexpected error occurred." : exception.Message,
            Detail = status == 500 ? null : exception.Message,
            Instance = context.Request.Path
        };
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
