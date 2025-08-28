namespace NICETaskDafna.Api.Matching;

/// A lexicon entry mapping one task to a list of phrases/variants that should trigger this task.
/// Immutable on purpose (safe for caching / sharing across requests).
public sealed record LexiconEntry(string Task, IReadOnlyList<string> Phrases);
