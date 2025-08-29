// File: Infra/ErrorHandling.cs
using System.Net;
using System.Text.Json;
using System.Linq; // GroupBy/Select/ToDictionary
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NICETaskDafna.Api.Infra; 

namespace NICETaskDafna.Api.Infra;

/// Global error handling middleware.
/// Translates exceptions into uniform JSON error responses,
/// and logs them with structured metadata.
/// Notable choices:
/// - Include a 'traceId' in responses so clients can report issues with a concrete id.
/// - Use EventId (from LogEvents) to categorize log records.
/// - Clear the response and avoid writing if it has already started.
public class ErrorHandling : IMiddleware
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly ILogger<ErrorHandling> _logger;

    public ErrorHandling(ILogger<ErrorHandling> logger) => _logger = logger;

    public async Task InvokeAsync(HttpContext ctx, RequestDelegate next)
    {
        try
        {
            await next(ctx);
        }
        catch (ValidationException vex)
        {
            // Categorized warning for invalid input (400)
            _logger.LogWarning(
                LogEvents.ValidationFailed,
                vex,
                "Validation error on {Path}",
                ctx.Request.Path
            );

            if (!ctx.Response.HasStarted)
            {
                ctx.Response.Clear();
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                ctx.Response.ContentType = "application/json";

                // Group validator errors by property to produce a clean payload.
                var details = vex.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

                var payload = new
                {
                    error = "Invalid input",
                    details,
                    traceId = ctx.TraceIdentifier
                };

                await ctx.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOpts));
            }
        }
        catch (Exception ex)
        {
            // Categorized error for unexpected failures (500)
            _logger.LogError(
                LogEvents.UnhandledException,
                ex,
                "Unhandled error on {Path}",
                ctx.Request.Path
            );

            if (!ctx.Response.HasStarted)
            {
                ctx.Response.Clear();
                ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
                ctx.Response.ContentType = "application/json";

                var payload = new
                {
                    error = "Server error",
                    traceId = ctx.TraceIdentifier
                };

                await ctx.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOpts));
            }
        }
    }
}
