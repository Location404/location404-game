using Location404.Game.Infrastructure.Cache;
using Xunit;

namespace Location404.Game.Application.UnitTests.Infrastructure.Cache;

public class InMemoryPlayerConnectionManagerTests
{
    [Fact]
    public async Task MapPlayerToConnectionAsync_ShouldStoreMapping()
    {
        // Arrange
        var manager = new InMemoryPlayerConnectionManager();
        var playerId = Guid.NewGuid();
        var connectionId = "connection-123";

        // Act
        await manager.MapPlayerToConnectionAsync(playerId, connectionId);

        // Assert
        var result = await manager.GetConnectionIdAsync(playerId);
        Assert.Equal(connectionId, result);
    }

    [Fact]
    public async Task GetConnectionIdAsync_WhenPlayerNotMapped_ShouldReturnNull()
    {
        // Arrange
        var manager = new InMemoryPlayerConnectionManager();
        var playerId = Guid.NewGuid();

        // Act
        var result = await manager.GetConnectionIdAsync(playerId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task MapPlayerToConnectionAsync_WhenPlayerAlreadyMapped_ShouldUpdateMapping()
    {
        // Arrange
        var manager = new InMemoryPlayerConnectionManager();
        var playerId = Guid.NewGuid();
        var oldConnectionId = "connection-old";
        var newConnectionId = "connection-new";

        await manager.MapPlayerToConnectionAsync(playerId, oldConnectionId);

        // Act
        await manager.MapPlayerToConnectionAsync(playerId, newConnectionId);

        // Assert
        var result = await manager.GetConnectionIdAsync(playerId);
        Assert.Equal(newConnectionId, result);
    }

    [Fact]
    public async Task RemoveMappingAsync_ShouldRemoveMapping()
    {
        // Arrange
        var manager = new InMemoryPlayerConnectionManager();
        var playerId = Guid.NewGuid();
        var connectionId = "connection-123";

        await manager.MapPlayerToConnectionAsync(playerId, connectionId);

        // Act
        await manager.RemoveMappingAsync(playerId);

        // Assert
        var result = await manager.GetConnectionIdAsync(playerId);
        Assert.Null(result);
    }

    [Fact]
    public async Task RemoveMappingAsync_WhenPlayerNotMapped_ShouldNotThrowException()
    {
        // Arrange
        var manager = new InMemoryPlayerConnectionManager();
        var playerId = Guid.NewGuid();

        // Act & Assert
        await manager.RemoveMappingAsync(playerId);
    }

    [Fact]
    public async Task MapPlayerToConnectionAsync_WithMultiplePlayers_ShouldMaintainSeparateMappings()
    {
        // Arrange
        var manager = new InMemoryPlayerConnectionManager();
        var player1Id = Guid.NewGuid();
        var player2Id = Guid.NewGuid();
        var connection1 = "connection-1";
        var connection2 = "connection-2";

        // Act
        await manager.MapPlayerToConnectionAsync(player1Id, connection1);
        await manager.MapPlayerToConnectionAsync(player2Id, connection2);

        // Assert
        var result1 = await manager.GetConnectionIdAsync(player1Id);
        var result2 = await manager.GetConnectionIdAsync(player2Id);
        Assert.Equal(connection1, result1);
        Assert.Equal(connection2, result2);
    }

    [Fact]
    public async Task RemoveMappingAsync_ShouldNotAffectOtherMappings()
    {
        // Arrange
        var manager = new InMemoryPlayerConnectionManager();
        var player1Id = Guid.NewGuid();
        var player2Id = Guid.NewGuid();
        var connection1 = "connection-1";
        var connection2 = "connection-2";

        await manager.MapPlayerToConnectionAsync(player1Id, connection1);
        await manager.MapPlayerToConnectionAsync(player2Id, connection2);

        // Act
        await manager.RemoveMappingAsync(player1Id);

        // Assert
        var result1 = await manager.GetConnectionIdAsync(player1Id);
        var result2 = await manager.GetConnectionIdAsync(player2Id);
        Assert.Null(result1);
        Assert.Equal(connection2, result2);
    }
}
