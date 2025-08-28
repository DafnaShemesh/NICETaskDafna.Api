// File: Program.cs
using System.Linq;
using System.Text.Json;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging; // LogLevel, AddJsonConsole, ActivityTrackingOptions
using NICETaskDafna.Api.Contracts;
using NICETaskDafna.Api.Contracts.Validation;
using NICETaskDafna.Api.Infra;
using NICETaskDafna.Api.Matching;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------
// Logging (structured JSON with sane defaults)
// Rationale:
// - IncludeScopes=true: allows correlationId/userId/sessionId to flow into each log line.
// - Indented in Dev only: human-friendly locally; compact in Prod for log ingestors.
// - Filter Microsoft* to Warning to reduce framework noise.
// - Keep our app (NICETaskDafna) at Information for good signal.
// - Limit ActivityTrackingOptions to TraceId (drop SpanId/ParentId) to cut noise,
//   while preserving cross-service correlation if needed.
// ---------------------------------------------------------
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(opt =>
{
    opt.IncludeScopes = true; // correlationId/userId/sessionId come from our scopes (middlewares/filters)
    opt.TimestampFormat = "O"; // ISO-8601 timestamps
    opt.JsonWriterOptions = new() { Indented = builder.Environment.IsDevelopment() };
});
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("NICETaskDafna", LogLevel.Information);
builder.Logging.Configure(o =>
{
    // Keep only TraceId from Activity; drop SpanId/ParentId to reduce clutter.
    o.ActivityTrackingOptions = ActivityTrackingOptions.TraceId;
});

// ---------------------------------------------------------
// Controllers & Validation
// - Global action filter enriches logs with userId + masked sessionId AFTER model binding.
// - Consistent 400 envelope for ModelState/Validation failures keeps client contract stable.
// ---------------------------------------------------------
builder.Services.AddScoped<LoggingEnrichmentFilter>();

builder.Services
    .AddControllers(opt =>
    {
        opt.Filters.AddService<LoggingEnrichmentFilter>();
    })
    .AddJsonOptions(_ => { });

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var details = context.ModelState
            .Where(kvp => kvp.Value?.Errors.Count > 0)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
            );

        var payload = new ErrorResponse(
            Error: "Invalid input",
            Details: details,
            TraceId: context.HttpContext.TraceIdentifier
        );

        return new BadRequestObjectResult(payload);
    };
});

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<SuggestTaskRequestValidator>();

// ---------------------------------------------------------
// Domain services
// - Abstract behind ITaskMatcher so we can swap implementations later
//   (advanced matcher, synonyms, retry) without touching controllers.
// ---------------------------------------------------------
builder.Services.AddScoped<ITaskMatcher, KeywordTaskMatcher>();

// ---------------------------------------------------------
// Cross-cutting infrastructure (pipeline order matters)
// 1) CorrelationId – generate/accept X-Request-ID and open logging scope.
// 2) RequestLogging – concise request/response summary (reduced noise).
// 3) ErrorHandling – translate exceptions to uniform JSON.
// ---------------------------------------------------------
builder.Services.AddTransient<CorrelationIdMiddleware>();
builder.Services.AddTransient<RequestLoggingMiddleware>();
builder.Services.AddTransient<ErrorHandling>();

// ---------------------------------------------------------
// Swagger (Dev only) – local API docs/playground; keep off in Prod.
// ---------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// If you do not run HTTPS locally, you can temporarily disable redirection in Dev to avoid warnings.
// if (app.Environment.IsDevelopment())
// {
//     // app.UseHttpsRedirection();
// }
app.UseHttpsRedirection();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ErrorHandling>();

// ---------------------------------------------------------
// Status code pages
// For "dry" framework-generated statuses (404/405/415/406/401/403), return a JSON envelope
// so the API remains consistent even without a controller body.
// ---------------------------------------------------------
app.UseStatusCodePages(async context =>
{
    var resp = context.HttpContext.Response;
    var traceId = context.HttpContext.TraceIdentifier;

    if (resp.HasStarted || resp.ContentLength.HasValue) return;

    string message = resp.StatusCode switch
    {
        404 => "Not found",
        405 => "Method not allowed",
        415 => "Unsupported media type",
        406 => "Not acceptable",
        401 => "Unauthorized",
        403 => "Forbidden",
        _   => "Request error"
    };

    var payload = new ErrorResponse(message, null, traceId);
    resp.ContentType = "application/json";
    await resp.WriteAsync(JsonSerializer.Serialize(payload));
});

app.MapControllers();
app.Run();

// Required by WebApplicationFactory for integration tests.
public partial class Program { }
