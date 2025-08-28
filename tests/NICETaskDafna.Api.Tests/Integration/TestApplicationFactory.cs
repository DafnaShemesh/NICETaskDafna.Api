// File: tests/NICETaskDafna.Api.Tests/Integration/TestApplicationFactory.cs

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using NICETaskDafna.Api.Services;  // ILexiconService
using NICETaskDafna.Api.Matching;  // LexiconEntry

namespace NICETaskDafna.Api.Tests.Integration;

/// <summary>
/// WebApplicationFactory that bootstraps the real pipeline in-memory,
/// but replaces ILexiconService with a deterministic test double
/// so integration tests are stable (no random failures or retries).
/// </summary>
public sealed class TestApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove any existing ILexiconService registration (simulated/cached)
            var toRemove = services
                .Where(d => d.ServiceType == typeof(ILexiconService))
                .ToList();
            foreach (var d in toRemove)
                services.Remove(d);

            // Register deterministic test double (see class below)
            services.AddSingleton<ILexiconService, TestLexiconService>();
        });
    }
}

/// <summary>
/// Deterministic test double for ILexiconService.
/// - Always returns the same rich lexicon (no random failures).
/// - Lets tests verify external matching without flakiness.
/// NOTE: Different name & namespace from production types to avoid CS0436 conflicts.
/// </summary>
internal sealed class TestLexiconService : ILexiconService
{
    public Task<IReadOnlyList<LexiconEntry>> GetLexiconAsync(string utterance, CancellationToken ct = default)
    {
        // Minimal but useful set: includes common typos to prove extended matching
        var reset = new[]
        {
            "reset password", "forgot password", "i forgot my password",
            "password reset", "change my password", "passwrod reset", // typo
            "reset my pass"
        };

        var order = new[]
        {
            "check order", "track order", "order status",
            "chek order", // typo
            "delivery status"
        };

        IReadOnlyList<LexiconEntry> list = new[]
        {
            new LexiconEntry("ResetPasswordTask", reset),
            new LexiconEntry("CheckOrderStatusTask", order),
        };

        return Task.FromResult(list);
    }
}
