using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Polly; 
using Xunit; 

using NICETaskDafna.Api.Matching;  
using NICETaskDafna.Api.Services;  

namespace NICETaskDafna.Api.Tests.Unit;

/// Unit tests for TwoTierTaskMatcher:
/// - Internal dictionary matches fast.
/// - External lexicon is consulted if internal fails.
/// - Null/empty input → "NoTaskFound".
/// - Retry: policy retries on transient failures and eventually succeeds.
/// - Cache: external calls should not repeat for the same utterance.

public class TwoTierTaskMatcherTests
{
    private static IAsyncPolicy<IReadOnlyList<LexiconEntry>> NoOpPolicy
        => Policy.NoOpAsync<IReadOnlyList<LexiconEntry>>();

    private static ILogger<TwoTierTaskMatcher> Logger => NullLogger<TwoTierTaskMatcher>.Instance;

    [Fact]
    public void InternalMap_Matches_ResetPasswordTask()
    {
        Console.WriteLine("[UNIT] InternalMap_Matches_ResetPasswordTask - should hit internal map only");
        var utterance = "I FORGOT my password!!";
        var external = new Mock<ILexiconService>(MockBehavior.Strict); 
        var sut = new TwoTierTaskMatcher(Logger, external.Object, NoOpPolicy);

        var task = sut.Match(utterance);

        task.Should().Be("ResetPasswordTask");
        external.Verify(x => x.GetLexiconAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ExternalLexicon_Matches_CheckOrderStatusTask_When_Internal_Fails()
    {
        Console.WriteLine("[UNIT] ExternalLexicon_Matches_CheckOrderStatusTask_When_Internal_Fails");
        var utterance = "pls chek order asap";
        var external = new Mock<ILexiconService>();
        external
            .Setup(x => x.GetLexiconAsync(utterance, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LexiconEntry>
            {
                new("CheckOrderStatusTask", new [] { "check order", "chek order", "order status" })
            });

        var sut = new TwoTierTaskMatcher(Logger, external.Object, NoOpPolicy);

        var task = sut.Match(utterance);

        task.Should().Be("CheckOrderStatusTask");
        external.Verify(x => x.GetLexiconAsync(utterance, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void NoMatch_Returns_NoTaskFound()
    {
        Console.WriteLine("[UNIT] NoMatch_Returns_NoTaskFound");
        var utterance = "how to open a new account";
        var external = new Mock<ILexiconService>();
        external.Setup(x => x.GetLexiconAsync(utterance, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<LexiconEntry>());

        var sut = new TwoTierTaskMatcher(Logger, external.Object, NoOpPolicy);

        var task = sut.Match(utterance);

        task.Should().Be("NoTaskFound");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NullOrWhitespace_Returns_NoTaskFound(string? utterance)
    {
        Console.WriteLine("[UNIT] NullOrWhitespace_Returns_NoTaskFound");
        var external = new Mock<ILexiconService>(MockBehavior.Strict);
        var sut = new TwoTierTaskMatcher(Logger, external.Object, NoOpPolicy);

        var task = sut.Match(utterance!);

        task.Should().Be("NoTaskFound");
        external.VerifyNoOtherCalls();
    }

    [Fact]
    public void RetryPolicy_Retries_Then_Succeeds()
    {
        Console.WriteLine("[UNIT] RetryPolicy_Retries_Then_Succeeds - external fails twice then succeeds");
        var utterance = "chek order please";

        var external = new Mock<ILexiconService>();
        external
            .SetupSequence(x => x.GetLexiconAsync(utterance, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("transient-1"))
            .ThrowsAsync(new Exception("transient-2"))
            .ReturnsAsync(new List<LexiconEntry> {
                new("CheckOrderStatusTask", new [] { "chek order", "check order" })
            });

        var retryPolicy = Policy<IReadOnlyList<LexiconEntry>>
            .Handle<Exception>()
            .WaitAndRetryAsync(2, _ => TimeSpan.FromMilliseconds(1));

        var sut = new TwoTierTaskMatcher(Logger, external.Object, retryPolicy);

        var task = sut.Match(utterance);

        task.Should().Be("CheckOrderStatusTask");
        external.Verify(x => x.GetLexiconAsync(utterance, It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public void Cache_WrappedService_ReturnsFromCache_OnSecondMatch()
    {
        Console.WriteLine("[UNIT] Cache_WrappedService_ReturnsFromCache_OnSecondMatch");
        var counter = 0;
        var fakeInner = new Mock<ILexiconService>();
        fakeInner
            .Setup(x => x.GetLexiconAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((u, _) =>
            {
                Interlocked.Increment(ref counter);
                IReadOnlyList<LexiconEntry> data = new List<LexiconEntry>
                {
                    new("CheckOrderStatusTask", new []{"chek order","check order"})
                };
                return Task.FromResult(data);
            });

        var cached = new SimpleTestCacheLexiconService(fakeInner.Object);
        var sut = new TwoTierTaskMatcher(Logger, cached, NoOpPolicy);

        var utterance = "chek order asap";
        var t1 = sut.Match(utterance);
        var t2 = sut.Match(utterance);

        t1.Should().Be("CheckOrderStatusTask");
        t2.Should().Be("CheckOrderStatusTask");
        counter.Should().Be(1, "second call should come from cache");

        Console.WriteLine($"[UNIT] Cache counter = {counter} (expected: 1)");
    }

    private sealed class SimpleTestCacheLexiconService : ILexiconService
    {
        private readonly ILexiconService _inner;
        private readonly Dictionary<string, IReadOnlyList<LexiconEntry>> _cache = new(StringComparer.OrdinalIgnoreCase);

        public SimpleTestCacheLexiconService(ILexiconService inner) => _inner = inner;

        public async Task<IReadOnlyList<LexiconEntry>> GetLexiconAsync(string utterance, CancellationToken ct = default)
        {
            if (_cache.TryGetValue(utterance, out var hit))
                return hit;

            var data = await _inner.GetLexiconAsync(utterance, ct);
            _cache[utterance] = data;
            return data;
        }
    }
}
