using System.Globalization;
using System.Text;

namespace NICETaskDafna.Api.Matching;

/// Text normalization utility:
/// - NFKC normalization
/// - Lowercase (Invariant)
/// - Strip diacritics
/// - Collapse whitespaces
/// This improves robustness for keyword matching.

public static class TextNormalizer
{
    public static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var nfkc = input.Normalize(NormalizationForm.FormKC)
                        .ToLower(CultureInfo.InvariantCulture);

        var sb = new StringBuilder(nfkc.Length);
        foreach (var ch in nfkc.Normalize(NormalizationForm.FormD))
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        var noDiacritics = sb.ToString().Normalize(NormalizationForm.FormC);
        return System.Text.RegularExpressions.Regex.Replace(noDiacritics, @"\s+", " ").Trim();
    }
}
