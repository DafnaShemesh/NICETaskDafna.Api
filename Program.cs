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
using NICETaskDafna.Api.Services; // external lexicon + cache services

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------
// Logging (structured JSON with sane defaults)
// Rationale:
// - IncludeScopes=true: correlationId/userId/sessionId flow into each log line.
// - Indented in Dev only: readable locally; compact in Prod.
// - Filter Microsoft* to Warning to reduce framework noise.
// - BUT: explicitly re-enable Microsoft.Hosting.Lifetime at Information
//   so you still see "Now listening on..." and "Application started...".
// - Track only TraceId to cut noise but keep cross-service correlation.
// ---------------------------------------------------------
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(opt =>
{
    opt.IncludeScopes = true;             // correlationId/userId/sessionId from our scopes
    opt.TimestampFormat = "O";            // ISO-8601 timestamps
    opt.JsonWriterOptions = new() { Indented = builder.Environment.IsDevelopment() };
});
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Information); // <-- Option 2: show startup lines again
builder.Logging.AddFilter("NICETaskDafna", LogLevel.Information);
builder.Logging.Configure(o =>
{
    o.ActivityTrackingOptions = ActivityTrackingOptions.TraceId; // drop SpanId/ParentId
});

// ---------------------------------------------------------
// Controllers & Validation
// - Global action filter enriches logs AFTER model binding with userId + masked sessionId.
// - Consistent 400 envelope for ModelState/Validation failures.
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
// Matching (Stage 2): Two-tier strategy
// 1) INTERNAL moderately-expanded map (fast, always available).
// 2) EXTERNAL rich lexicon (simulated) wrapped by in-memory cache (fresh+stale)
//    and Polly (timeout + retry + fallback) inside the matcher.
// ---------------------------------------------------------

// In-process cache (used by the cached lexicon decorator)
builder.Services.AddMemoryCache();

// External rich lexicon (simulated) + cache decorator
builder.Services.AddSingleton<SimulatedExternalLexiconService>();

// Resolve ILexiconService to the cached decorator that wraps the simulated external service
builder.Services.AddSingleton<ILexiconService>(sp =>
    new CachedSynonymService(
        sp.GetRequiredService<SimulatedExternalLexiconService>(),
        sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>()));

// Use the new two-tier matcher (Internal → External). This replaces KeywordTaskMatcher.
builder.Services.AddScoped<ITaskMatcher, TwoTierTaskMatcher>();

// ---------------------------------------------------------
// Cross-cutting infrastructure (pipeline order matters)
// 1) CorrelationId – generate/accept X-Request-ID and open logging scope.
// 2) RequestLogging – concise request/response summary.
// 3) ErrorHandling – translate exceptions to uniform JSON.
// ---------------------------------------------------------
builder.Services.AddTransient<CorrelationIdMiddleware>();
builder.Services.AddTransient<RequestLoggingMiddleware>();
builder.Services.AddTransient<ErrorHandling>();

// ---------------------------------------------------------
// Swagger (Dev only) – local API docs/playground
// ---------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// If you do not run HTTPS locally, you can temporarily disable redirection in Dev.
// if (app.Environment.IsDevelopment())
// {
//     // app.UseHttpsRedirection();
// }
app.UseHttpsRedirection();

// Middleware order: Correlation → Request logging → Error handling
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ErrorHandling>();

// ---------------------------------------------------------
// Status code pages
// For framework-generated statuses (404/405/415/406/401/403) return a JSON envelope
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
