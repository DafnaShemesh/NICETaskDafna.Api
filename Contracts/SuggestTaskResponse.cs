namespace NICETaskDafna.Api.Contracts;

public record SuggestTaskResponse(
    string Task,
    DateTime Timestamp
);
