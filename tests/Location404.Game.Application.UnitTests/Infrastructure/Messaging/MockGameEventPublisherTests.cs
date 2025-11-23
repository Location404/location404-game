using Location404.Game.Application.DTOs;
using Location404.Game.Application.Events;
using Location404.Game.Infrastructure.Messaging;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Location404.Game.Application.UnitTests.Infrastructure.Messaging;

public class MockGameEventPublisherTests
{
    private readonly ILogger<MockGameEventPublisher> _logger;

    public MockGameEventPublisherTests()
    {
        _logger = Substitute.For<ILogger<MockGameEventPublisher>>();
    }

    [Fact]
    public async Task PublishMatchEndedAsync_ShouldLogEvent()
    {
        // Arrange
        var publisher = new MockGameEventPublisher(_logger);
        var matchEndedEvent = new GameMatchEndedEvent(
            MatchId: Guid.NewGuid(),
            PlayerAId: Guid.NewGuid(),
            PlayerBId: Guid.NewGuid(),
            WinnerId: Guid.NewGuid(),
            LoserId: Guid.NewGuid(),
            PlayerATotalPoints: 15000,
            PlayerBTotalPoints: 12000,
            PointsEarned: 100,
            PointsLost: 50,
            StartTime: DateTime.UtcNow.AddHours(-1),
            EndTime: DateTime.UtcNow,
            Rounds: new List<GameRoundDto>()
        );

        // Act
        await publisher.PublishMatchEndedAsync(matchEndedEvent);

        // Assert
        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Match Ended Event")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task PublishRoundEndedAsync_ShouldLogEvent()
    {
        // Arrange
        var publisher = new MockGameEventPublisher(_logger);
        var roundEndedEvent = new GameRoundEndedEvent(
            MatchId: Guid.NewGuid(),
            RoundId: Guid.NewGuid(),
            RoundNumber: 1,
            PlayerAId: Guid.NewGuid(),
            PlayerBId: Guid.NewGuid(),
            GameResponse: new CoordinateDto(X: -23.5505, Y: -46.6333),
            PlayerAGuess: new CoordinateDto(X: -23.5510, Y: -46.6340),
            PlayerBGuess: new CoordinateDto(X: -23.5500, Y: -46.6330),
            PlayerAPoints: 5000,
            PlayerBPoints: 4500,
            WinnerId: Guid.NewGuid(),
            EndTime: DateTime.UtcNow
        );

        // Act
        await publisher.PublishRoundEndedAsync(roundEndedEvent);

        // Assert
        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Round Ended Event")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public void Constructor_WhenLoggerIsNull_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MockGameEventPublisher(null!));
    }

    [Fact]
    public async Task PublishMatchEndedAsync_ShouldIncludeMatchIdInLog()
    {
        // Arrange
        var publisher = new MockGameEventPublisher(_logger);
        var matchId = Guid.NewGuid();
        var matchEndedEvent = new GameMatchEndedEvent(
            MatchId: matchId,
            PlayerAId: Guid.NewGuid(),
            PlayerBId: Guid.NewGuid(),
            WinnerId: Guid.NewGuid(),
            LoserId: Guid.NewGuid(),
            PlayerATotalPoints: 15000,
            PlayerBTotalPoints: 12000,
            PointsEarned: 100,
            PointsLost: 50,
            StartTime: DateTime.UtcNow.AddHours(-1),
            EndTime: DateTime.UtcNow,
            Rounds: new List<GameRoundDto>()
        );

        // Act
        await publisher.PublishMatchEndedAsync(matchEndedEvent);

        // Assert
        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains(matchId.ToString())),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task PublishRoundEndedAsync_ShouldIncludeMatchIdAndRoundNumberInLog()
    {
        // Arrange
        var publisher = new MockGameEventPublisher(_logger);
        var matchId = Guid.NewGuid();
        var roundNumber = 2;
        var roundEndedEvent = new GameRoundEndedEvent(
            MatchId: matchId,
            RoundId: Guid.NewGuid(),
            RoundNumber: roundNumber,
            PlayerAId: Guid.NewGuid(),
            PlayerBId: Guid.NewGuid(),
            GameResponse: new CoordinateDto(X: -23.5505, Y: -46.6333),
            PlayerAGuess: new CoordinateDto(X: -23.5510, Y: -46.6340),
            PlayerBGuess: new CoordinateDto(X: -23.5500, Y: -46.6330),
            PlayerAPoints: 5000,
            PlayerBPoints: 4500,
            WinnerId: Guid.NewGuid(),
            EndTime: DateTime.UtcNow
        );

        // Act
        await publisher.PublishRoundEndedAsync(roundEndedEvent);

        // Assert
        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains(matchId.ToString()) && o.ToString()!.Contains(roundNumber.ToString())),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }
}
