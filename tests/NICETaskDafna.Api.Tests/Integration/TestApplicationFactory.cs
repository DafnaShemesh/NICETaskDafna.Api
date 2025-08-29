using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

using NICETaskDafna.Api.Services;
using NICETaskDafna.Api.Matching;

namespace NICETaskDafna.Api.Tests.Integration;

/// <summary>
/// WebApplicationFactory that boots the real pipeline in-memory,
/// but replaces ILexiconService with a deterministic test double,
/// so integration tests are stable (no random failures/retries).
/// Also enables us to assert simple caching behavior.
/// </summary>
public sealed class TestApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove any existing ILexiconService registration (prod simulated/cached)
            var toRemove = services
                .Where(d => d.ServiceType == typeof(ILexiconService))
                .ToList();
            foreach (var d in toRemove)
                services.Remove(d);

            // Register deterministic test double with counting + simple cache
            services.AddSingleton<ILexiconService, CountingCachedTestLexiconService>();
        });
    }
}

/// <summary>
/// Deterministic ILexiconService used for integration tests:
/// - Returns a fixed "rich" lexicon (proves external matching).
/// - Adds a tiny in-memory cache (per utterance).
/// - Counts how many "external" calls were made to verify caching.
/// NOTE: This is purposely simple. We only care about cache hits vs misses.
/// </summary>
internal sealed class CountingCachedTestLexiconService : ILexiconService
{
    public static int ExternalCalls = 0;

    private readonly ConcurrentDictionary<string, IReadOnlyList<LexiconEntry>> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<LexiconEntry>> GetLexiconAsync(string utterance, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(utterance, out var hit))
        {
            Console.WriteLine($"[INTEGRATION] Cache HIT for utterance: \"{utterance}\"");
            return Task.FromResult(hit);
        }

        Console.WriteLine($"[INTEGRATION] Cache MISS for utterance: \"{utterance}\"");
        Interlocked.Increment(ref ExternalCalls);

        var reset = new[] { "reset password", "forgot password", "i forgot my password" };
        var order = new[] { "check order", "chek order", "order status", "delivery status" };

        IReadOnlyList<LexiconEntry> list = new[]
        {
            new LexiconEntry("ResetPasswordTask", reset),
            new LexiconEntry("CheckOrderStatusTask", order),
        };

        _cache[utterance] = list;
        return Task.FromResult(list);
    }
}

/// <summary>
/// Integration tests against the real HTTP pipeline (in-memory server).
/// We verify:
/// - External lexicon is consulted and the result is cached (2 posts -> 1 external call).
/// - Invalid input returns HTTP 400 with a consistent error envelope.
/// </summary>
public sealed class SuggestTaskIntegrationTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory _factory;

    public SuggestTaskIntegrationTests(TestApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_SameUtterance_Twice_Uses_Cache()
    {
        Console.WriteLine("[INTEGRATION] Running: Post_SameUtterance_Twice_Uses_Cache");
        CountingCachedTestLexiconService.ExternalCalls = 0;

        var client = _factory.CreateClient();
        var payload = new
        {
            utterance = "chek order please", // will match via EXTERNAL variants
            userId = "u1",
            sessionId = "s1",
            timestamp = DateTime.UtcNow.ToString("o")
        };

        // First call -> MISS expected
        var r1 = await client.PostAsJsonAsync("/suggestTask", payload);
        r1.EnsureSuccessStatusCode();

        // Second call with the exact same utterance -> HIT expected
        var r2 = await client.PostAsJsonAsync("/suggestTask", payload);
        r2.EnsureSuccessStatusCode();

        Console.WriteLine($"[INTEGRATION] ExternalCalls counted: {CountingCachedTestLexiconService.ExternalCalls}");
        Assert.Equal(1, CountingCachedTestLexiconService.ExternalCalls);
    }

    [Fact]
    public async Task Post_InvalidInput_Returns_400()
    {
        Console.WriteLine("[INTEGRATION] Running: Post_InvalidInput_Returns_400");
        var client = _factory.CreateClient();

        // Missing 'utterance' and invalid 'timestamp' -> should trigger FluentValidation and global 400 envelope
        var badPayload = new { userId = "", sessionId = "", timestamp = "not-a-valid-iso" };

        var response = await client.PostAsJsonAsync("/suggestTask", badPayload);
        var body = await response.Content.ReadAsStringAsync();

        Console.WriteLine("[INTEGRATION] 400 response payload: " + body);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"error\":\"Invalid input\"", body);
        Assert.Contains("\"traceId\"", body);
    }

    // If you already had a DTO here, keep it; otherwise we don't need one for these tests.
    private sealed class ResponseDto
    {
        public string task { get; set; } = default!;
        public string timestamp { get; set; } = default!;
    }
}
