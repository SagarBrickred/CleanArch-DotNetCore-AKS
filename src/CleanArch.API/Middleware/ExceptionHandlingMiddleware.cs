using CleanArch.Application.Common.Exceptions;
using System.Net;
using System.Text.Json;
using ValidationException = CleanArch.Application.Common.Exceptions.ValidationException;

namespace CleanArch.API.Middleware;

/// <summary>
/// Central exception-to-HTTP-response translator. Never leaks stack traces or internal exception
/// messages to the client in production; everything is logged (and flows to App Insights) with full detail server-side.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = context.TraceIdentifier;

        var (statusCode, title) = exception switch
        {
            ValidationException => (HttpStatusCode.BadRequest, "Validation failed"),
            NotFoundException => (HttpStatusCode.NotFound, "Resource not found"),
            ConflictException => (HttpStatusCode.Conflict, "Conflict"),
            UnauthorizedAccessException => (HttpStatusCode.Forbidden, "Forbidden"),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred")
        };

        if (statusCode == HttpStatusCode.InternalServerError)
            _logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}", traceId);
        else
            _logger.LogWarning(exception, "Handled exception ({StatusCode}). TraceId: {TraceId}", statusCode, traceId);

        var problemDetails = new Dictionary<string, object?>
        {
            ["type"] = $"https://httpstatuses.io/{(int)statusCode}",
            ["title"] = title,
            ["status"] = (int)statusCode,
            ["traceId"] = traceId
        };

        if (exception is ValidationException validationException)
            problemDetails["errors"] = validationException.Errors;

        // Only surface exception detail in non-production environments — a hard rule, not a toggle left to config.
        if (!_env.IsProduction())
            problemDetails["detail"] = exception.Message;

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails));
    }
}
