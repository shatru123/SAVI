using System.Net;
using System.Text.Json;

namespace SAVI.Api.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred during request execution.");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var friendlyResponse = new
        {
            success = false,
            message = "I ran into an unexpected problem while processing that request. The error has been safely logged.",
            code = "INTERNAL_SERVER_ERROR"
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(friendlyResponse));
    }
}
