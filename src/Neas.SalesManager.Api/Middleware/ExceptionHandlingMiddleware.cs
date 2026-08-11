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
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            // Unique constraint or duplicate primary key violation
            _logger.LogError(ex, "SQL Engine Constraint Violation: Duplicate entry or primary assignment conflict.");
            await WriteErrorResponseAsync(context, HttpStatusCode.Conflict, "A primary salesperson is already assigned or duplicate entry exists.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Unhandled exception encountered during execution.");
            await WriteErrorResponseAsync(context, HttpStatusCode.InternalServerError, "An unexpected server error occurred.");
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