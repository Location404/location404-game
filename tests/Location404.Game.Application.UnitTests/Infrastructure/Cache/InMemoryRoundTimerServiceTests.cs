using Location404.Game.Infrastructure.Cache;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Location404.Game.Application.UnitTests.Infrastructure.Cache;

public class InMemoryRoundTimerServiceTests
{
    private readonly ILogger<InMemoryRoundTimerService> _logger;

    public InMemoryRoundTimerServiceTests()
    {
        _logger = Substitute.For<ILogger<InMemoryRoundTimerService>>();
    }

    [Fact]
    public async Task StartTimerAsync_ShouldStartTimer()
    {
        // Arrange
        var service = new InMemoryRoundTimerService(_logger);
        var matchId = Guid.NewGuid();
        var roundId = Guid.NewGuid();

        // Act
        await service.StartTimerAsync(matchId, roundId, TimeSpan.FromSeconds(10));

        // Assert
        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Started") && o.ToString()!.Contains("10")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task CancelTimerAsync_WhenTimerExists_ShouldCancelTimer()
    {
        // Arrange
        var service = new InMemoryRoundTimerService(_logger);
        var matchId = Guid.NewGuid();
        var roundId = Guid.NewGuid();

        await service.StartTimerAsync(matchId, roundId, TimeSpan.FromSeconds(10));

        // Act
        await service.CancelTimerAsync(matchId, roundId);

        // Assert
        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Cancelled timer")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task CancelTimerAsync_WhenTimerDoesNotExist_ShouldNotThrowException()
    {
        // Arrange
        var service = new InMemoryRoundTimerService(_logger);
        var matchId = Guid.NewGuid();
        var roundId = Guid.NewGuid();

        // Act
        var act = async () => await service.CancelTimerAsync(matchId, roundId);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetRemainingTimeAsync_ShouldReturnNull()
    {
        // Arrange
        var service = new InMemoryRoundTimerService(_logger);
        var matchId = Guid.NewGuid();
        var roundId = Guid.NewGuid();

        await service.StartTimerAsync(matchId, roundId, TimeSpan.FromSeconds(10));

        // Act
        var result = await service.GetRemainingTimeAsync(matchId, roundId);

        // Assert
        Assert.Null(result);
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("GetRemainingTimeAsync not implemented")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task AdjustTimerAsync_ShouldCancelAndRestartTimer()
    {
        // Arrange
        var service = new InMemoryRoundTimerService(_logger);
        var matchId = Guid.NewGuid();
        var roundId = Guid.NewGuid();

        await service.StartTimerAsync(matchId, roundId, TimeSpan.FromSeconds(10));

        // Act
        await service.AdjustTimerAsync(matchId, roundId, TimeSpan.FromSeconds(5));

        // Assert
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("AdjustTimerAsync not implemented")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );

        // Should have cancelled old timer and started new one
        _logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Cancelled timer")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );

        _logger.Received(2).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Started")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task StartTimerAsync_WithMultipleTimers_ShouldMaintainSeparateTimers()
    {
        // Arrange
        var service = new InMemoryRoundTimerService(_logger);
        var match1Id = Guid.NewGuid();
        var match2Id = Guid.NewGuid();
        var round1Id = Guid.NewGuid();
        var round2Id = Guid.NewGuid();

        // Act
        await service.StartTimerAsync(match1Id, round1Id, TimeSpan.FromSeconds(10));
        await service.StartTimerAsync(match2Id, round2Id, TimeSpan.FromSeconds(5));

        // Assert - should have started 2 timers
        _logger.Received(2).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Started")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task CancelTimerAsync_ShouldOnlyCancelSpecificTimer()
    {
        // Arrange
        var service = new InMemoryRoundTimerService(_logger);
        var match1Id = Guid.NewGuid();
        var match2Id = Guid.NewGuid();
        var round1Id = Guid.NewGuid();
        var round2Id = Guid.NewGuid();

        await service.StartTimerAsync(match1Id, round1Id, TimeSpan.FromSeconds(10));
        await service.StartTimerAsync(match2Id, round2Id, TimeSpan.FromSeconds(10));

        // Act
        await service.CancelTimerAsync(match1Id, round1Id);

        // Assert - should have cancelled only one timer
        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Cancelled timer")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task StartTimerAsync_WhenTimerExpires_ShouldLogWarning()
    {
        // Arrange
        var service = new InMemoryRoundTimerService(_logger);
        var matchId = Guid.NewGuid();
        var roundId = Guid.NewGuid();

        // Act
        await service.StartTimerAsync(matchId, roundId, TimeSpan.FromMilliseconds(50));
        await Task.Delay(100); // Wait for timer to expire

        // Assert
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Timer expired")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task StartTimerAsync_CanBeCalledMultipleTimes_ForSameMatchAndRound()
    {
        // Arrange
        var service = new InMemoryRoundTimerService(_logger);
        var matchId = Guid.NewGuid();
        var roundId = Guid.NewGuid();

        // Act - start timer twice for same match/round
        await service.StartTimerAsync(matchId, roundId, TimeSpan.FromSeconds(10));
        await service.StartTimerAsync(matchId, roundId, TimeSpan.FromSeconds(5));

        // Assert - should have started 2 timers (replaces the first one)
        _logger.Received(2).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Started")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }
}
