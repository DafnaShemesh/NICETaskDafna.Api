namespace NICETaskDafna.Api.Contracts;

public sealed record ErrorResponse(
    string Error,
    Dictionary<string, string[]>? Details = null,
    string? TraceId = null
);