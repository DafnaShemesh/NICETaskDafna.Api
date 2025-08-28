using Microsoft.Extensions.Caching.Memory;
using NICETaskDafna.Api.Matching;

namespace NICETaskDafna.Api.Services;

/// Cache decorator around an external lexicon (synonyms):
/// - "fresh" (short TTL) for fast repeats
/// - "stale" (longer TTL) for resilient last-known-good fallback
/// Prevents repeated external calls and avoids visible failures when upstream is flaky.
public sealed class CachedSynonymService : ILexiconService
{
    private readonly ILexiconService _inner;
    private readonly IMemoryCache _cache;

    public CachedSynonymService(ILexiconService inner, IMemoryCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<IReadOnlyList<LexiconEntry>> GetLexiconAsync(string utterance, CancellationToken ct = default)
    {
        var norm = TextNormalizer.Normalize(utterance);
        var freshKey = $"lex:fresh:{norm}";
        var staleKey = $"lex:stale:{norm}";

        if (_cache.TryGetValue(freshKey, out IReadOnlyList<LexiconEntry>? fresh) && fresh is not null)
            return fresh;

        try
        {
            var result = await _inner.GetLexiconAsync(utterance, ct);

            _cache.Set(freshKey, result, TimeSpan.FromMinutes(5));   // fast path
            _cache.Set(staleKey, result, TimeSpan.FromHours(1));     // last-known-good
            return result;
        }
        catch when (!ct.IsCancellationRequested)
        {
            if (_cache.TryGetValue(staleKey, out IReadOnlyList<LexiconEntry>? stale) && stale is not null)
                return stale; // serve last-known-good if available

            // no fallback available; let upstream failure bubble (to Polly or caller)
            throw;
        }
    }
}
