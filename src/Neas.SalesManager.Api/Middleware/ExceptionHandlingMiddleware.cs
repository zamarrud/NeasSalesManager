// src/Neas.SalesManager.Api/Middleware/ExceptionHandlingMiddleware.cs
using System.Net;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Neas.SalesManager.Api.Middleware;

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
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Domain validation failure.");
            await WriteErrorResponseAsync(context, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "SQL Exception encountered: {Message}", ex.Message);
            await WriteErrorResponseAsync(context, HttpStatusCode.InternalServerError, $"SQL Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Unhandled exception encountered.");
            await WriteErrorResponseAsync(context, HttpStatusCode.InternalServerError, ex.Message);
        }
    }

    private static Task WriteErrorResponseAsync(HttpContext context, HttpStatusCode statusCode, string message)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            StatusCode = context.Response.StatusCode,
            Message = message,
            TimestampUtc = DateTime.UtcNow
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}