// File: Infra/LogEvents.cs
using Microsoft.Extensions.Logging;

namespace NICETaskDafna.Api.Infra;

/// Central catalog of EventIds used across the project.
/// Helps avoid duplication and keeps logs consistent.
public static class LogEvents
{
    // Pipeline / Hosting
    public static readonly EventId RequestStart       = new(1001, nameof(RequestStart));
    public static readonly EventId RequestCompleted   = new(1002, nameof(RequestCompleted));

    // Validation / Errors
    public static readonly EventId ValidationFailed   = new(2001, nameof(ValidationFailed));
    public static readonly EventId UnhandledException = new(2002, nameof(UnhandledException));

    // Matching (TwoTierTaskMatcher)
    public static readonly EventId MatchFoundInternal = new(3001, nameof(MatchFoundInternal));
    public static readonly EventId MatchFoundExternal = new(3002, nameof(MatchFoundExternal));
    public static readonly EventId NoMatch            = new(3003, nameof(NoMatch));
}
