using System.Linq;
using NICETaskDafna.Api.Matching;

namespace NICETaskDafna.Api.Services;

/// Simulated external lexicon provider:
/// - Returns a richer set of phrases per task (core + colloquial + common typos).
/// - Randomly fails (~30%) to emulate transient upstream issues (for Retry/Cache demo).
/// Notes:
/// - I explicitly include the four "core" keys required by the assignment.
/// - I add real-world variants and frequent misspellings so extended matching
/// - Every phrase is normalized (lowercased, diacritics stripped, whitespace collapsed)

public sealed class SimulatedExternalLexiconService : ILexiconService
{
    private static readonly Random Rng = new();

    public Task<IReadOnlyList<LexiconEntry>> GetLexiconAsync(string utterance, CancellationToken ct = default)
    {
        // Simulate transient failure to exercise Timeout/Retry/Fallback policies.
        if (Rng.NextDouble() < 0.3)
            throw new HttpRequestException("External lexicon failed (simulated).");

        var resetCore = new[]
        {
            "reset password",
            "forgot password"
        };

        // Natural variants users often write:
        var resetVariants = new[]
        {
            "i forgot my password",
            "password reset",
            "change my password",
            "resetting my password",
            "reset my pass",
            "recover my account",
            "account recovery",
            "cant login to my account",   
            "can't log in to my account",
            "cant log into my account",
            "can't log into my account",
            "pass code",
            "passcode"
        };

        // Common misspellings 
        var resetTypos = new[]
        {
            "reser password",       // missing 't'
            "rest password",        // dropped 'e'
            "reset pasword",        // missing 's'
            "reset passwrod",       // transposed 'r'/'o'
            "forgor password",      // meme/typo for "forgot"
            "forgot pasword",
            "forgot passwrod",
            "i fogrot my password", // swapped letters
            "pssword reset",        // missing 'a'
            "pass codee"            // extra letter
        };

        // Core keys (must be present per the assignment):
        var orderCore = new[]
        {
            "check order",
            "track order"
        };

        // Natural variants users often write:
        var orderVariants = new[]
        {
            "where is my order",
            "order status",
            "track my package",
            "package status",
            "delivery status",
            "shipping status",
            "parcel",
            "track shipment",
            "where is my shipment",
            "tracking number"
        };

        // Common misspellings 
        var orderTypos = new[]
        {
            // "check order" typos
            "chek order",           // missing 'c'
            "check oder",           // missing 'r'
            "chcek order",          // letters swapped
            // "track order" typos
            "trak order",           // missing 'c'
            "trakc order",          // swapped 'c'/'k'
            "tarck order",          // swapped 'a'/'r'
            "track orde",           // missing 'r'
            // package/shipping/delivery typos
            "track pacakge",        // swapped 'a'/'k'
            "pakage status",        // missing 'c'
            "shiping status",       // missing 'p'
            "delivary status",      // a/e swap
            "parsel",               // 'sel' vs 'cel'
            "traking number",       // missing 'c'
            "where is my shipmet"   // missing 'n'
        };

        // Build normalized, de-duplicated lists per task:
        var resetAll = resetCore
            .Concat(resetVariants)
            .Concat(resetTypos)
            .Select(TextNormalizer.Normalize)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var orderAll = orderCore
            .Concat(orderVariants)
            .Concat(orderTypos)
            .Select(TextNormalizer.Normalize)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var list = new List<LexiconEntry>
        {
            new("ResetPasswordTask", resetAll),
            new("CheckOrderStatusTask", orderAll)
        };

        return Task.FromResult<IReadOnlyList<LexiconEntry>>(list);
    }
}
