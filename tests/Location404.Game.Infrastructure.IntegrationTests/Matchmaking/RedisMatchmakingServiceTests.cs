using Location404.Game.Application.Features.GameRounds.Interfaces;
using Location404.Game.Domain.Entities;
using Location404.Game.Infrastructure.Matchmaking;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Location404.Game.Infrastructure.IntegrationTests.Matchmaking;

public class RedisMatchmakingServiceTests : IClassFixture<RedisFixture>
{
    private readonly RedisFixture _fixture;
    private readonly ILogger<RedisMatchmakingService> _logger;

    public RedisMatchmakingServiceTests(RedisFixture fixture)
    {
        _fixture = fixture;
        _logger = Substitute.For<ILogger<RedisMatchmakingService>>();
    }

    private async Task ClearQueueAsync()
    {
        var db = _fixture.Redis.GetDatabase();
        await db.KeyDeleteAsync("matchmaking:queue");
        await db.KeyDeleteAsync("matchmaking:players");
    }

    [Fact]
    public async Task JoinQueueAsync_ShouldAddPlayerToQueue()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var mockManager = Substitute.For<IGameMatchManager>();
        mockManager.IsPlayerInMatchAsync(playerId).Returns(false);
        var service = new RedisMatchmakingService(_fixture.Redis, mockManager, _logger);

        // Act
        var result = await service.JoinQueueAsync(playerId);

        // Assert
        Assert.Equal(playerId, result);
        var inQueue = await service.IsPlayerInQueueAsync(playerId);
        Assert.True(inQueue);
        var queueSize = await service.GetQueueSizeAsync();
        Assert.Equal(1, queueSize);

        // Cleanup
        await service.LeaveQueueAsync(playerId);
    }

    [Fact]
    public async Task JoinQueueAsync_WhenPlayerAlreadyInMatch_ShouldThrowException()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var mockManager = Substitute.For<IGameMatchManager>();
        mockManager.IsPlayerInMatchAsync(playerId).Returns(true);
        var service = new RedisMatchmakingService(_fixture.Redis, mockManager, _logger);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.JoinQueueAsync(playerId)
        );
    }

    [Fact]
    public async Task JoinQueueAsync_WhenPlayerAlreadyInQueue_ShouldThrowException()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var mockManager = Substitute.For<IGameMatchManager>();
        mockManager.IsPlayerInMatchAsync(playerId).Returns(false);
        var service = new RedisMatchmakingService(_fixture.Redis, mockManager, _logger);

        await service.JoinQueueAsync(playerId);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.JoinQueueAsync(playerId)
        );

        // Cleanup
        await service.LeaveQueueAsync(playerId);
    }

    [Fact]
    public async Task LeaveQueueAsync_ShouldRemovePlayerFromQueue()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var mockManager = Substitute.For<IGameMatchManager>();
        mockManager.IsPlayerInMatchAsync(playerId).Returns(false);
        var service = new RedisMatchmakingService(_fixture.Redis, mockManager, _logger);

        await service.JoinQueueAsync(playerId);

        // Act
        await service.LeaveQueueAsync(playerId);

        // Assert
        var inQueue = await service.IsPlayerInQueueAsync(playerId);
        Assert.False(inQueue);
    }

    [Fact]
    public async Task GetQueueSizeAsync_ShouldReturnCorrectSize()
    {
        // Arrange
        var player1Id = Guid.NewGuid();
        var player2Id = Guid.NewGuid();
        var player3Id = Guid.NewGuid();
        var mockManager = Substitute.For<IGameMatchManager>();
        mockManager.IsPlayerInMatchAsync(Arg.Any<Guid>()).Returns(false);
        var service = new RedisMatchmakingService(_fixture.Redis, mockManager, _logger);

        // Act
        await service.JoinQueueAsync(player1Id);
        await service.JoinQueueAsync(player2Id);
        await service.JoinQueueAsync(player3Id);

        // Assert
        var queueSize = await service.GetQueueSizeAsync();
        Assert.Equal(3, queueSize);

        // Cleanup
        await service.LeaveQueueAsync(player1Id);
        await service.LeaveQueueAsync(player2Id);
        await service.LeaveQueueAsync(player3Id);
    }

    [Fact]
    public async Task IsPlayerInQueueAsync_WhenPlayerNotInQueue_ShouldReturnFalse()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var mockManager = Substitute.For<IGameMatchManager>();
        var service = new RedisMatchmakingService(_fixture.Redis, mockManager, _logger);

        // Act
        var result = await service.IsPlayerInQueueAsync(playerId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsPlayerInQueueAsync_WhenPlayerInQueue_ShouldReturnTrue()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var mockManager = Substitute.For<IGameMatchManager>();
        mockManager.IsPlayerInMatchAsync(playerId).Returns(false);
        var service = new RedisMatchmakingService(_fixture.Redis, mockManager, _logger);

        await service.JoinQueueAsync(playerId);

        // Act
        var result = await service.IsPlayerInQueueAsync(playerId);

        // Assert
        Assert.True(result);

        // Cleanup
        await service.LeaveQueueAsync(playerId);
    }

    [Fact]
    public async Task TryFindMatchAsync_WhenLessThanTwoPlayers_ShouldReturnNull()
    {
        // Arrange
        await ClearQueueAsync();
        var playerId = Guid.NewGuid();
        var mockManager = Substitute.For<IGameMatchManager>();
        mockManager.IsPlayerInMatchAsync(playerId).Returns(false);
        var service = new RedisMatchmakingService(_fixture.Redis, mockManager, _logger);

        await service.JoinQueueAsync(playerId);

        // Act
        var result = await service.TryFindMatchAsync();

        // Assert
        Assert.Null(result);

        // Cleanup
        await service.LeaveQueueAsync(playerId);
    }

    [Fact]
    public async Task TryFindMatchAsync_WhenTwoOrMorePlayers_ShouldCreateMatch()
    {
        // Arrange
        await ClearQueueAsync();
        var player1Id = Guid.NewGuid();
        var player2Id = Guid.NewGuid();
        var mockManager = Substitute.For<IGameMatchManager>();
        mockManager.IsPlayerInMatchAsync(Arg.Any<Guid>()).Returns(false);

        var expectedMatch = GameMatch.StartGameMatch(player1Id, player2Id);
        mockManager.CreateMatchAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(expectedMatch);

        var service = new RedisMatchmakingService(_fixture.Redis, mockManager, _logger);

        await service.JoinQueueAsync(player1Id);
        await service.JoinQueueAsync(player2Id);

        // Act
        var result = await service.TryFindMatchAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(player1Id, result.PlayerAId);
        Assert.Equal(player2Id, result.PlayerBId);

        // Verify players were removed from queue
        var player1InQueue = await service.IsPlayerInQueueAsync(player1Id);
        var player2InQueue = await service.IsPlayerInQueueAsync(player2Id);
        Assert.False(player1InQueue);
        Assert.False(player2InQueue);
    }

}
