using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NICETaskDafna.Api.Matching;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Timeout;

namespace NICETaskDafna.Api.Infra;

/// Resiliency policies for the external lexicon:
/// - Per-attempt timeout (1s)
/// - Jittered backoff retry (3 tries) with logging on each attempt
/// - Typed fallback: returns an EMPTY lexicon on final failure (no exception bubbles up)
/// Order matters: fallback(retry(timeout()))

public static class RetryPolicies
{
    public static IAsyncPolicy<IReadOnlyList<LexiconEntry>> CreateLexiconPolicy(ILogger logger)
    {
        var delays = Backoff.DecorrelatedJitterBackoffV2(
            medianFirstRetryDelay: TimeSpan.FromMilliseconds(150),
            retryCount: 3);

        // 1) Timeout per attempt (do not hang forever on a slow upstream)
        var timeout = Policy.TimeoutAsync<IReadOnlyList<LexiconEntry>>(TimeSpan.FromSeconds(1));

        // 2) Retry on transient failures; log each failed attempt with its delay
        var retry = Policy<IReadOnlyList<LexiconEntry>>
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>() 
            .WaitAndRetryAsync(
                delays,
                onRetry: (outcome, delay, attempt, ctx) =>
                {
                    var msg = outcome.Exception?.Message ?? "non-exception outcome";
                    logger.LogWarning(
                        "External lexicon attempt {Attempt} failed: {Message}. Retrying in {Delay}...",
                        attempt, msg, delay);
                });

        // 3) Fallback if all retries are exhausted; log the final fallback
        var fallback = Policy<IReadOnlyList<LexiconEntry>>
            .Handle<Exception>()
            .FallbackAsync(
                fallbackValue: Array.Empty<LexiconEntry>(),
                onFallbackAsync: (outcome, ctx) =>
                {
                    logger.LogError(
                        outcome.Exception,
                        "External lexicon failed after retries. Falling back to EMPTY lexicon.");
                    return Task.CompletedTask;
                });

        return Policy.WrapAsync(fallback, retry, timeout);
    }
}
