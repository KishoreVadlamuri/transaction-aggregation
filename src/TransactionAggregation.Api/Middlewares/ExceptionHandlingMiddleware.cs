using System.Diagnostics;
using System.Net;
using System.Text.Json;
using TransactionAggregation.Api.Models;

namespace TransactionAggregation.Api.Middlewares;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
        catch (ArgumentException ex)
        {
            _logger.LogWarning(
                ex,
                "Bad request for {Method} {Path}: {Message}",
                context.Request.Method,
                context.Request.Path,
                ex.Message);
            await WriteErrorAsync(context, HttpStatusCode.BadRequest, ex.Message, ex.Message);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Request cancelled by client for {Method} {Path}",
                context.Request.Method,
                context.Request.Path);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteErrorAsync(context, HttpStatusCode.InternalServerError, "An unexpected error occurred.", ex.Message);
        }
    }

    private async Task WriteErrorAsync(HttpContext context, HttpStatusCode status, string message, string? detail)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)status;

        var payload = new ApiError
        {
            Message = message,
            Detail = detail,
            TraceId = Activity.Current?.Id ?? context.TraceIdentifier
        };

        _logger.LogDebug(
            "Writing error response {StatusCode} for {Method} {Path} (TraceId={TraceId})",
            (int)status,
            context.Request.Method,
            context.Request.Path,
            payload.TraceId);

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}

