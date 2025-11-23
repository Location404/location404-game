using Location404.Game.Domain.Entities;
using Location404.Game.Infrastructure.Cache;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Location404.Game.Application.UnitTests.Infrastructure.Cache;

public class InMemoryGameMatchManagerTests
{
    private readonly ILogger<InMemoryGameMatchManager> _logger;

    public InMemoryGameMatchManagerTests()
    {
        _logger = Substitute.For<ILogger<InMemoryGameMatchManager>>();
    }

    [Fact]
    public async Task CreateMatchAsync_ShouldCreateAndStoreMatch()
    {
        // Arrange
        var manager = new InMemoryGameMatchManager(_logger);
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();

        // Act
        var match = await manager.CreateMatchAsync(playerAId, playerBId);

        // Assert
        Assert.NotNull(match);
        Assert.Equal(playerAId, match.PlayerAId);
        Assert.Equal(playerBId, match.PlayerBId);
    }

    [Fact]
    public async Task GetMatchAsync_WhenMatchExists_ShouldReturnMatch()
    {
        // Arrange
        var manager = new InMemoryGameMatchManager(_logger);
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var match = await manager.CreateMatchAsync(playerAId, playerBId);

        // Act
        var result = await manager.GetMatchAsync(match.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(match.Id, result.Id);
    }

    [Fact]
    public async Task GetMatchAsync_WhenMatchDoesNotExist_ShouldReturnNull()
    {
        // Arrange
        var manager = new InMemoryGameMatchManager(_logger);
        var matchId = Guid.NewGuid();

        // Act
        var result = await manager.GetMatchAsync(matchId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetPlayerCurrentMatchAsync_WhenPlayerInMatch_ShouldReturnMatch()
    {
        // Arrange
        var manager = new InMemoryGameMatchManager(_logger);
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var match = await manager.CreateMatchAsync(playerAId, playerBId);

        // Act
        var resultA = await manager.GetPlayerCurrentMatchAsync(playerAId);
        var resultB = await manager.GetPlayerCurrentMatchAsync(playerBId);

        // Assert
        Assert.NotNull(resultA);
        Assert.NotNull(resultB);
        Assert.Equal(match.Id, resultA.Id);
        Assert.Equal(match.Id, resultB.Id);
    }

    [Fact]
    public async Task GetPlayerCurrentMatchAsync_WhenPlayerNotInMatch_ShouldReturnNull()
    {
        // Arrange
        var manager = new InMemoryGameMatchManager(_logger);
        var playerId = Guid.NewGuid();

        // Act
        var result = await manager.GetPlayerCurrentMatchAsync(playerId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateMatchAsync_ShouldUpdateExistingMatch()
    {
        // Arrange
        var manager = new InMemoryGameMatchManager(_logger);
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var match = await manager.CreateMatchAsync(playerAId, playerBId);

        match.StartNewGameRound();
        var correctLocation = new Coordinate(x: -23.5505, y: -46.6333);
        var playerAGuess = new Coordinate(x: -23.5510, y: -46.6340);
        var playerBGuess = new Coordinate(x: -23.5500, y: -46.6330);
        match.EndCurrentGameRound(correctLocation, playerAGuess, playerBGuess);

        // Act
        await manager.UpdateMatchAsync(match);

        // Assert
        var result = await manager.GetMatchAsync(match.Id);
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalRounds);
    }

    [Fact]
    public async Task RemoveMatchAsync_ShouldRemoveMatch()
    {
        // Arrange
        var manager = new InMemoryGameMatchManager(_logger);
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var match = await manager.CreateMatchAsync(playerAId, playerBId);

        // Act
        await manager.RemoveMatchAsync(match.Id);

        // Assert
        var result = await manager.GetMatchAsync(match.Id);
        Assert.Null(result);
    }

    [Fact]
    public async Task RemoveMatchAsync_ShouldRemovePlayerMappings()
    {
        // Arrange
        var manager = new InMemoryGameMatchManager(_logger);
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var match = await manager.CreateMatchAsync(playerAId, playerBId);

        // Act
        await manager.RemoveMatchAsync(match.Id);

        // Assert
        var resultA = await manager.GetPlayerCurrentMatchAsync(playerAId);
        var resultB = await manager.GetPlayerCurrentMatchAsync(playerBId);
        Assert.Null(resultA);
        Assert.Null(resultB);
    }

    [Fact]
    public async Task RemoveMatchAsync_WhenMatchDoesNotExist_ShouldNotThrowException()
    {
        // Arrange
        var manager = new InMemoryGameMatchManager(_logger);
        var matchId = Guid.NewGuid();

        // Act & Assert
        await manager.RemoveMatchAsync(matchId);
    }

    [Fact]
    public async Task IsPlayerInMatchAsync_WhenPlayerInMatch_ShouldReturnTrue()
    {
        // Arrange
        var manager = new InMemoryGameMatchManager(_logger);
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        await manager.CreateMatchAsync(playerAId, playerBId);

        // Act
        var result = await manager.IsPlayerInMatchAsync(playerAId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsPlayerInMatchAsync_WhenPlayerNotInMatch_ShouldReturnFalse()
    {
        // Arrange
        var manager = new InMemoryGameMatchManager(_logger);
        var playerId = Guid.NewGuid();

        // Act
        var result = await manager.IsPlayerInMatchAsync(playerId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetAllActiveMatchIdsAsync_WhenNoMatches_ShouldReturnEmptyList()
    {
        // Arrange
        var manager = new InMemoryGameMatchManager(_logger);

        // Act
        var result = await manager.GetAllActiveMatchIdsAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllActiveMatchIdsAsync_WhenMultipleMatches_ShouldReturnAllMatchIds()
    {
        // Arrange
        var manager = new InMemoryGameMatchManager(_logger);
        var match1 = await manager.CreateMatchAsync(Guid.NewGuid(), Guid.NewGuid());
        var match2 = await manager.CreateMatchAsync(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await manager.GetAllActiveMatchIdsAsync();

        // Assert
        Assert.Equal(2, result.Count());
        Assert.Contains(match1.Id, result);
        Assert.Contains(match2.Id, result);
    }

    [Fact]
    public async Task ClearPlayerMatchStateAsync_ShouldRemovePlayerMapping()
    {
        // Arrange
        var manager = new InMemoryGameMatchManager(_logger);
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        await manager.CreateMatchAsync(playerAId, playerBId);

        // Act
        await manager.ClearPlayerMatchStateAsync(playerAId);

        // Assert
        var result = await manager.GetPlayerCurrentMatchAsync(playerAId);
        Assert.Null(result);
    }

    [Fact]
    public async Task ClearPlayerMatchStateAsync_ShouldNotAffectMatch()
    {
        // Arrange
        var manager = new InMemoryGameMatchManager(_logger);
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var match = await manager.CreateMatchAsync(playerAId, playerBId);

        // Act
        await manager.ClearPlayerMatchStateAsync(playerAId);

        // Assert
        var matchResult = await manager.GetMatchAsync(match.Id);
        Assert.NotNull(matchResult);
    }

    [Fact]
    public async Task ClearPlayerMatchStateAsync_ShouldNotAffectOtherPlayer()
    {
        // Arrange
        var manager = new InMemoryGameMatchManager(_logger);
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var match = await manager.CreateMatchAsync(playerAId, playerBId);

        // Act
        await manager.ClearPlayerMatchStateAsync(playerAId);

        // Assert
        var resultB = await manager.GetPlayerCurrentMatchAsync(playerBId);
        Assert.NotNull(resultB);
        Assert.Equal(match.Id, resultB.Id);
    }

    [Fact]
    public void Constructor_WhenLoggerIsNull_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new InMemoryGameMatchManager(null!));
    }
}
