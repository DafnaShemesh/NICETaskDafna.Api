using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using NICETaskDafna.Api.Contracts;

namespace NICETaskDafna.Api.Infra;

    /// Action filter that enriches all logs with userId and sessionId.
    /// Utility method to mask sensitive strings (e.g. sessionId).
    /// Keeps only the last 4 characters visible, replaces the rest with '*'.
    /// Example: "abcde-67890" -> "*******7890".
    /// Balances security (no sensitive leakage) with traceability (able to
    /// distinguish between sessions).

public class LoggingEnrichmentFilter : IActionFilter
{
    private readonly ILogger<LoggingEnrichmentFilter> _logger;
    private IDisposable? _scope;

    public LoggingEnrichmentFilter(ILogger<LoggingEnrichmentFilter> logger)
    {
        _logger = logger;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ActionArguments.TryGetValue("request", out var val) && val is SuggestTaskRequest req)
        {
            _scope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["userId"] = req.UserId,
                ["sessionId"] = Mask(req.SessionId),
            });
            _logger.LogInformation("Enriched logging scope with user/session");
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        _scope?.Dispose();
    }

    private static string Mask(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Length <= 4 ? "****" : new string('*', Math.Max(0, s.Length - 4)) + s[^4..];
    }
}
