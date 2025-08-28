using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace NICETaskDafna.Api.Infra;

/// Middleware that ensures every incoming request has a unique Correlation ID to trace an errors.  
// Accept an existing "X-Request-ID" header if the client or API Gateway sets it,
///   otherwise generate a new GUID. This preserves trace continuity across services.

public class CorrelationIdMiddleware : IMiddleware
{
    public const string HeaderName = "X-Request-ID";
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(ILogger<CorrelationIdMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // Either reuse client-provided X-Request-ID or generate a new GUID.
        var cid = context.Request.Headers.TryGetValue(HeaderName, out var existing) && 
                  !string.IsNullOrWhiteSpace(existing)
            ? existing.ToString()
            : Guid.NewGuid().ToString("N");

        context.Response.Headers[HeaderName] = cid;

        if (Activity.Current is null) Activity.Current = new Activity("IncomingRequest").Start();
        Activity.Current?.AddTag("correlation_id", cid);

        // Begin a logging scope so all logs automatically include correlationId.
        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["correlationId"] = cid
        }))
        {
            // Continue to the next middleware in the pipeline.
            await next(context);
        }
    }
}
