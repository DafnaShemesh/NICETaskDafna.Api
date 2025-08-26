using System.Globalization;

namespace NICETaskDafna.Api.Matching;

// Simple keyword-based matcher implementation
public class KeywordTaskMatcher : ITaskMatcher
{
    private static readonly (string Key, string Task)[] Map =
    {
        ("reset password",   "ResetPasswordTask"),
        ("forgot password",  "ResetPasswordTask"),
        ("check order",      "CheckOrderStatusTask"),
        ("track order",      "CheckOrderStatusTask"),
    };

    public string Match(string utterance)
    {
        if (string.IsNullOrWhiteSpace(utterance))
            return "NoTaskFound";

        var text = utterance.ToLower(CultureInfo.InvariantCulture);

        foreach (var (key, task) in Map)
        {
            if (text.Contains(key))
                return task;
        }

        return "NoTaskFound";
    }
}
