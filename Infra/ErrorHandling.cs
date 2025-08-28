using System.Net;
using System.Text.Json;
using System.Linq; // GroupBy/Select/ToDictionary
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace NICETaskDafna.Api.Infra;

/// Global error handling middleware.
/// Translates exceptions into uniform JSON error responses,
/// and logs them with structured metadata.
/// Notable choices:
/// - I include a 'traceId' in responses so clients can report issues with a concrete id.
/// - I use EventId in logs.
public class ErrorHandling : IMiddleware
{
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
            // Use EventId to categorize validation failures in logging/monitoring tools.
            _logger.LogWarning(
                new EventId(1001, "ValidationFailed"),
                vex,
                "Validation error on {Path}",
                ctx.Request.Path
            );

            ctx.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            ctx.Response.ContentType = "application/json";

            // Group validator errors by property to produce a clean payload.
            var details = vex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            // Keep the error envelope consistent with other 400/500 responses
            // by including traceId to aid support/debugging.
            var payload = new
            {
                error = "Invalid input",
                details,
                traceId = ctx.TraceIdentifier
            };

            await ctx.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
        catch (Exception ex)
        {
            // EventId distinguishes "unexpected/unhandled" failures from validation issues.
            _logger.LogError(
                new EventId(1002, "UnhandledException"),
                ex,
                "Unhandled error on {Path}",
                ctx.Request.Path
            );

            ctx.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            ctx.Response.ContentType = "application/json";

            var payload = new
            {
                error = "Server error",
                traceId = ctx.TraceIdentifier
            };

            await ctx.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
    }
}
