using NICETaskDafna.Api.Matching;

namespace NICETaskDafna.Api.Services;

/// Supplies "task -> phrases" entries.
/// Implementations: simulated external provider, cached decorator, etc.
public interface ILexiconService
{
    Task<IReadOnlyList<LexiconEntry>> GetLexiconAsync(string utterance, CancellationToken ct = default);
}
