// File: Matching/TwoTierTaskMatcher.cs
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using NICETaskDafna.Api.Infra;     // <- for RetryPolicies
using NICETaskDafna.Api.Services;  // <- ILexiconService
using Polly;

namespace NICETaskDafna.Api.Matching;

/// <summary>
/// Two-tier matching strategy:
/// 1) Try the INTERNAL moderately-expanded map (fast, always available).
/// 2) If not found, query the EXTERNAL rich lexicon (Polly: timeout + retry + fallback + cache).
/// If neither yields a match, return "NoTaskFound".
/// </summary>
public sealed class TwoTierTaskMatcher : ITaskMatcher
{
    private readonly ILogger<TwoTierTaskMatcher> _logger;
    private readonly ILexiconService _external;   // resolved via DI (implementation = CachedLexiconService)
    private readonly IAsyncPolicy<IReadOnlyList<LexiconEntry>> _policy;

    // Internal moderately-expanded map (normalized on the fly)
    private static readonly (string Key, string Task)[] InternalMap =
    {
        // Password (expanded but not too much)
        ("reset password",          "ResetPasswordTask"),
        ("forgot password",         "ResetPasswordTask"),
        ("i forgot my password",    "ResetPasswordTask"),
        ("password reset",          "ResetPasswordTask"),
        ("change my password",      "ResetPasswordTask"),
        ("pass code",               "ResetPasswordTask"),
        ("reset my pass",           "ResetPasswordTask"),

        // Order (expanded but not too much)
        ("check order",             "CheckOrderStatusTask"),
        ("track order",             "CheckOrderStatusTask"),
        ("where is my order",       "CheckOrderStatusTask"),
        ("order status",            "CheckOrderStatusTask"),
        ("track my package",        "CheckOrderStatusTask"),
        ("delivery status",         "CheckOrderStatusTask"),
        ("shipping status",         "CheckOrderStatusTask"),
        ("parcel",                  "CheckOrderStatusTask")
    };

    public TwoTierTaskMatcher(
        ILogger<TwoTierTaskMatcher> logger,
        ILexiconService external, // DI will provide CachedLexiconService as ILexiconService
        IAsyncPolicy<IReadOnlyList<LexiconEntry>>? policy = null)
    {
        _logger  = logger;
        _external = external;
        _policy   = policy ?? RetryPolicies.CreateLexiconPolicy(_logger); // <-- pass logger (signature expects it)
    }

    public string Match(string utterance)
    {
        if (string.IsNullOrWhiteSpace(utterance))
            return "NoTaskFound";

        var norm = TextNormalizer.Normalize(utterance);
        _logger.LogDebug("Normalized utterance: {Normalized}", norm);

        // 1) INTERNAL map (fast path)
        foreach (var (key, task) in InternalMap)
        {
            var k = TextNormalizer.Normalize(key);
            if (norm.Contains(k))
            {
                _logger.LogInformation("Matched via INTERNAL map: {Task} (key='{Key}')", task, key);
                return task;
            }
        }

        // 2) EXTERNAL rich lexicon (with Polly policy + cache)
        var externalLex = _policy.ExecuteAsync(
            (ctx, ct) => _external.GetLexiconAsync(utterance, ct),
            new Context("TwoTierTaskMatcher_GetLexicon"),
            CancellationToken.None
        ).GetAwaiter().GetResult();

        foreach (var entry in externalLex)
        {
            // provider already normalizes; normalize defensively anyway
            foreach (var phrase in entry.Phrases)
            {
                var p = TextNormalizer.Normalize(phrase);
                if (norm.Contains(p))
                {
                    _logger.LogInformation("Matched via EXTERNAL lexicon: {Task} (phrase='{Phrase}')", entry.Task, phrase);
                    return entry.Task;
                }
            }
        }

        return "NoTaskFound";
    }
}
