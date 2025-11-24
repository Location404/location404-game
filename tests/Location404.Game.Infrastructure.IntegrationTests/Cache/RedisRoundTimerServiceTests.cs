using Location404.Game.Infrastructure.Cache;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Location404.Game.Infrastructure.IntegrationTests.Cache;

public class RedisRoundTimerServiceTests : IClassFixture<RedisFixture>
{
    private readonly RedisFixture _fixture;
    private readonly ILogger<RedisRoundTimerService> _logger;

    public RedisRoundTimerServiceTests(RedisFixture fixture)
    {
        _fixture = fixture;
        _logger = Substitute.For<ILogger<RedisRoundTimerService>>();
    }

    [Fact]
    public async Task StartTimerAsync_ShouldCreateTimerInRedis()
    {
        // Arrange
        var service = new RedisRoundTimerService(_fixture.Redis, _logger);
        var matchId = Guid.NewGuid();
        var roundId = Guid.NewGuid();
        var duration = TimeSpan.FromSeconds(30);

        // Act
        await service.StartTimerAsync(matchId, roundId, duration);

        // Assert
        var remainingTime = await service.GetRemainingTimeAsync(matchId, roundId);
        Assert.NotNull(remainingTime);
        Assert.True(remainingTime.Value.TotalSeconds > 0);
        Assert.True(remainingTime.Value.TotalSeconds <= duration.TotalSeconds);

        // Cleanup
        await service.CancelTimerAsync(matchId, roundId);
    }

    [Fact]
    public async Task GetRemainingTimeAsync_WhenTimerDoesNotExist_ShouldReturnNull()
    {
        // Arrange
        var service = new RedisRoundTimerService(_fixture.Redis, _logger);
        var matchId = Guid.NewGuid();
        var roundId = Guid.NewGuid();

        // Act
        var result = await service.GetRemainingTimeAsync(matchId, roundId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CancelTimerAsync_ShouldRemoveTimer()
    {
        // Arrange
        var service = new RedisRoundTimerService(_fixture.Redis, _logger);
        var matchId = Guid.NewGuid();
        var roundId = Guid.NewGuid();

        await service.StartTimerAsync(matchId, roundId, TimeSpan.FromSeconds(60));

        // Act
        await service.CancelTimerAsync(matchId, roundId);

        // Assert
        var remainingTime = await service.GetRemainingTimeAsync(matchId, roundId);
        Assert.Null(remainingTime);
    }

    [Fact]
    public async Task AdjustTimerAsync_ShouldUpdateTimerDuration()
    {
        // Arrange
        var service = new RedisRoundTimerService(_fixture.Redis, _logger);
        var matchId = Guid.NewGuid();
        var roundId = Guid.NewGuid();

        await service.StartTimerAsync(matchId, roundId, TimeSpan.FromSeconds(60));
        await Task.Delay(100); // Wait a bit

        // Act
        await service.AdjustTimerAsync(matchId, roundId, TimeSpan.FromSeconds(30));

        // Assert
        var remainingTime = await service.GetRemainingTimeAsync(matchId, roundId);
        Assert.NotNull(remainingTime);
        Assert.True(remainingTime.Value.TotalSeconds <= 30);

        // Cleanup
        await service.CancelTimerAsync(matchId, roundId);
    }

    [Fact]
    public async Task AdjustTimerAsync_WhenTimerDoesNotExist_ShouldLogWarning()
    {
        // Arrange
        var service = new RedisRoundTimerService(_fixture.Redis, _logger);
        var matchId = Guid.NewGuid();
        var roundId = Guid.NewGuid();

        // Act
        await service.AdjustTimerAsync(matchId, roundId, TimeSpan.FromSeconds(30));

        // Assert
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Cannot adjust timer")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task StartTimerAsync_WithMultipleTimers_ShouldMaintainSeparateTimers()
    {
        // Arrange
        var service = new RedisRoundTimerService(_fixture.Redis, _logger);
        var match1Id = Guid.NewGuid();
        var match2Id = Guid.NewGuid();
        var round1Id = Guid.NewGuid();
        var round2Id = Guid.NewGuid();

        // Act
        await service.StartTimerAsync(match1Id, round1Id, TimeSpan.FromSeconds(60));
        await service.StartTimerAsync(match2Id, round2Id, TimeSpan.FromSeconds(30));

        // Assert
        var timer1 = await service.GetRemainingTimeAsync(match1Id, round1Id);
        var timer2 = await service.GetRemainingTimeAsync(match2Id, round2Id);

        Assert.NotNull(timer1);
        Assert.NotNull(timer2);

        // Cleanup
        await service.CancelTimerAsync(match1Id, round1Id);
        await service.CancelTimerAsync(match2Id, round2Id);
    }

    [Fact]
    public async Task GetRemainingTimeAsync_ShouldReturnDecreasingTime()
    {
        // Arrange
        var service = new RedisRoundTimerService(_fixture.Redis, _logger);
        var matchId = Guid.NewGuid();
        var roundId = Guid.NewGuid();

        await service.StartTimerAsync(matchId, roundId, TimeSpan.FromSeconds(10));

        // Act
        var time1 = await service.GetRemainingTimeAsync(matchId, roundId);
        await Task.Delay(1000); // Wait 1 second
        var time2 = await service.GetRemainingTimeAsync(matchId, roundId);

        // Assert
        Assert.NotNull(time1);
        Assert.NotNull(time2);
        Assert.True(time2.Value < time1.Value);

        // Cleanup
        await service.CancelTimerAsync(matchId, roundId);
    }
}
