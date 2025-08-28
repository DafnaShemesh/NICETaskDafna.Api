// File: tests/NICETaskDafna.Api.Tests/Unit/TwoTierTaskMatcherTests.cs

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Polly; // <- needed for Policy.NoOpAsync
using Xunit; // <- needed for [Fact]/[Theory]

using NICETaskDafna.Api.Matching;  // TwoTierTaskMatcher, LexiconEntry, ITaskMatcher
using NICETaskDafna.Api.Services;  // ILexiconService

namespace NICETaskDafna.Api.Tests.Unit;

/// <summary>
/// Unit tests for TwoTierTaskMatcher:
/// - INTERNAL map matches fast for known phrases.
/// - EXTERNAL lexicon is consulted if internal fails.
/// - Null/empty inputs → "NoTaskFound".
/// 
/// We inject:
/// - Mocked ILexiconService (deterministic; no randomness)
/// - NoOp Polly policy (so unit tests don't actually retry/wait)
/// </summary>
public class TwoTierTaskMatcherTests
{
    private static IAsyncPolicy<IReadOnlyList<LexiconEntry>> NoOpPolicy
        => Policy.NoOpAsync<IReadOnlyList<LexiconEntry>>();

    private static ILogger<TwoTierTaskMatcher> Logger => NullLogger<TwoTierTaskMatcher>.Instance;

    [Fact]
    public void InternalMap_Matches_ResetPasswordTask()
    {
        // Arrange: utterance that should hit the INTERNAL map
        var utterance = "I FORGOT my password!!"; // mixed case + punctuation
        var external = new Mock<ILexiconService>(MockBehavior.Strict); // should not be called
        var sut = new TwoTierTaskMatcher(Logger, external.Object, NoOpPolicy);

        // Act
        var task = sut.Match(utterance);

        // Assert
        task.Should().Be("ResetPasswordTask");
        external.Verify(x => x.GetLexiconAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ExternalLexicon_Matches_CheckOrderStatusTask_When_Internal_Fails()
    {
        // Arrange: utterance NOT in internal map but present in external variants (typo)
        var utterance = "pls chek order asap";
        var external = new Mock<ILexiconService>();
        external
            .Setup(x => x.GetLexiconAsync(utterance, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LexiconEntry>
            {
                new("CheckOrderStatusTask", new [] { "check order", "chek order", "order status" })
            });

        var sut = new TwoTierTaskMatcher(Logger, external.Object, NoOpPolicy);

        // Act
        var task = sut.Match(utterance);

        // Assert
        task.Should().Be("CheckOrderStatusTask");
        external.Verify(x => x.GetLexiconAsync(utterance, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void NoMatch_Returns_NoTaskFound()
    {
        // Arrange
        var utterance = "how to open a new account";
        var external = new Mock<ILexiconService>();
        // external returns empty lexicon -> still no match
        external.Setup(x => x.GetLexiconAsync(utterance, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<LexiconEntry>());

        var sut = new TwoTierTaskMatcher(Logger, external.Object, NoOpPolicy);

        // Act
        var task = sut.Match(utterance);

        // Assert
        task.Should().Be("NoTaskFound");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NullOrWhitespace_Returns_NoTaskFound(string? utterance)
    {
        var external = new Mock<ILexiconService>(MockBehavior.Strict);
        var sut = new TwoTierTaskMatcher(Logger, external.Object, NoOpPolicy);

        var task = sut.Match(utterance!);

        task.Should().Be("NoTaskFound");
        external.VerifyNoOtherCalls();
    }
}
