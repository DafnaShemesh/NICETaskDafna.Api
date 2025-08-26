namespace NICETaskDafna.Api.Matching;

// Contract for any "Task Matcher" implementation
public interface ITaskMatcher
{
    string Match(string utterance);
}