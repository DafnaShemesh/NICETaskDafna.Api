using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace NICETaskDafna.Api.Infra;

/// Lightweight HTTP request/response logging middleware.
/// Keep logs informative (status code, latency, UA, IP) without being noisy.
/// Let correlationId/userId/sessionId flow from other components (scopes).
/// Avoid clutter from trivial browser paths ("/", "/favicon.ico").
/// 
/// Design choices:
/// - "started" is logged at DEBUG (visible only when explicitly enabled).
/// - "completed" summary is logged at INFORMATION (good signal-to-noise).
/// - We do not log request/response bodies here to avoid PII leakage.

public class RequestLoggingMiddleware : IMiddleware
{
    private static readonly HashSet<string> _skipPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/", "/favicon.ico"
    };

    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(ILogger<RequestLoggingMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // Skip known noisy paths to reduce log clutter during development.
        if (_skipPaths.Contains(context.Request.Path))
        {
            await next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        var req = context.Request;

        // DEBUG-level "started" to help with concurrency investigations without spamming normal logs.
        _logger.LogDebug("HTTP {Method} {Path} started", req.Method, req.Path);

        try
        {
            await next(context);
        }
        finally
        {
            sw.Stop();
            var res = context.Response;

            // INFORMATION-level "completed" summarizing the request outcome.
            _logger.LogInformation(
                "HTTP {Method} {Path} completed {StatusCode} in {ElapsedMs} ms {Length}B UA={UserAgent} IP={RemoteIp}",
                req.Method,
                req.Path,
                res.StatusCode,
                sw.ElapsedMilliseconds,
                res.ContentLength ?? 0,
                req.Headers.UserAgent.ToString(),
                context.Connection.RemoteIpAddress?.ToString()
            );
        }
    }
}

