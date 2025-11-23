using Location404.Game.Infrastructure.Cache;
using Xunit;

namespace Location404.Game.Infrastructure.IntegrationTests.Cache;

public class PlayerConnectionManagerTests : IClassFixture<RedisFixture>
{
    private readonly RedisFixture _fixture;

    public PlayerConnectionManagerTests(RedisFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MapPlayerToConnectionAsync_ShouldStoreMapping()
    {
        // Arrange
        var manager = new PlayerConnectionManager(_fixture.Redis);
        var playerId = Guid.NewGuid();
        var connectionId = "connection-123";

        // Act
        await manager.MapPlayerToConnectionAsync(playerId, connectionId);

        // Assert
        var result = await manager.GetConnectionIdAsync(playerId);
        Assert.Equal(connectionId, result);

        // Cleanup
        await manager.RemoveMappingAsync(playerId);
    }

    [Fact]
    public async Task GetConnectionIdAsync_WhenPlayerNotMapped_ShouldReturnNull()
    {
        // Arrange
        var manager = new PlayerConnectionManager(_fixture.Redis);
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
        var manager = new PlayerConnectionManager(_fixture.Redis);
        var playerId = Guid.NewGuid();
        var oldConnectionId = "connection-old";
        var newConnectionId = "connection-new";

        await manager.MapPlayerToConnectionAsync(playerId, oldConnectionId);

        // Act
        await manager.MapPlayerToConnectionAsync(playerId, newConnectionId);

        // Assert
        var result = await manager.GetConnectionIdAsync(playerId);
        Assert.Equal(newConnectionId, result);

        // Cleanup
        await manager.RemoveMappingAsync(playerId);
    }

    [Fact]
    public async Task RemoveMappingAsync_ShouldRemoveMapping()
    {
        // Arrange
        var manager = new PlayerConnectionManager(_fixture.Redis);
        var playerId = Guid.NewGuid();
        var connectionId = "connection-123";

        await manager.MapPlayerToConnectionAsync(playerId, connectionId);

        // Act
        await manager.RemoveMappingAsync(playerId);

        // Assert
        var result = await manager.GetConnectionIdAsync(playerId);
        Assert.Null(result);
    }
}
