namespace NICETaskDafna.Api.Contracts;

public record SuggestTaskRequest(
    string Utterance,
    string UserId,
    string SessionId,
    DateTime Timestamp
);