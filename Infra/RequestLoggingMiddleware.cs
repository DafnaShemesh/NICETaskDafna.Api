using System.Diagnostics;
using System.Collections.Generic; 
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
/// - EventId constants (RequestStart/RequestCompleted/UnhandledError) to make logs queryable.
/// - Severity by status code: 2xx=Information, 4xx=Warning, 5xx=Error.
/// - Error path: catches unhandled exceptions, logs with EventId, rethrows.

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

        _logger.LogDebug(LogEvents.RequestStart, 
            "HTTP {Method} {Path} started",
            req.Method, req.Path);

        try
        {
            await next(context);
        }
        catch (Exception ex) 
        {
            sw.Stop();
            _logger.LogError(LogEvents.UnhandledException, ex,
                "HTTP {Method} {Path} failed with 500 in {ElapsedMs} ms UA={UserAgent} IP={RemoteIp}",
                req.Method,
                req.Path,
                sw.ElapsedMilliseconds,
                req.Headers.UserAgent.ToString(),
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown");

            throw; 
        }
        finally
        {
            if (sw.IsRunning) sw.Stop();
        }

        var res = context.Response;

        //"completed" summarizing the request outcome.
        var status = res.StatusCode;
        var length = res.ContentLength ?? 0L;

        if (status >= 500)
        {
            _logger.LogError(LogEvents.RequestCompleted,
                "HTTP {Method} {Path} completed {StatusCode} in {ElapsedMs} ms {Length}B UA={UserAgent} IP={RemoteIp}",
                req.Method,
                req.Path,
                status,
                sw.ElapsedMilliseconds,
                length,
                req.Headers.UserAgent.ToString(),
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        }
        else if (status >= 400)
        {
            _logger.LogWarning(LogEvents.RequestCompleted,
                "HTTP {Method} {Path} completed {StatusCode} in {ElapsedMs} ms {Length}B UA={UserAgent} IP={RemoteIp}",
                req.Method,
                req.Path,
                status,
                sw.ElapsedMilliseconds,
                length,
                req.Headers.UserAgent.ToString(),
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        }
        else
        {
            _logger.LogInformation(LogEvents.RequestCompleted,
                "HTTP {Method} {Path} completed {StatusCode} in {ElapsedMs} ms {Length}B UA={UserAgent} IP={RemoteIp}",
                req.Method,
                req.Path,
                status,
                sw.ElapsedMilliseconds,
                length,
                req.Headers.UserAgent.ToString(),
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        }
    }
}
