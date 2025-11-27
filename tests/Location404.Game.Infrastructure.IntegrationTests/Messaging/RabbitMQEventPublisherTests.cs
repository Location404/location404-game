using Location404.Game.Application.DTOs;
using Location404.Game.Application.Events;
using Location404.Game.Domain.Entities;
using Location404.Game.Infrastructure.Configuration;
using Location404.Game.Infrastructure.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;

namespace Location404.Game.Infrastructure.IntegrationTests.Messaging;

public class RabbitMQEventPublisherTests : IClassFixture<RabbitMQFixture>
{
    private readonly RabbitMQFixture _fixture;
    private readonly ILogger<RabbitMQEventPublisher> _logger;

    public RabbitMQEventPublisherTests(RabbitMQFixture fixture)
    {
        _fixture = fixture;
        _logger = Substitute.For<ILogger<RabbitMQEventPublisher>>();
    }

    private RabbitMQEventPublisher CreatePublisher()
    {
        var settings = Options.Create(new RabbitMQSettings
        {
            HostName = _fixture.HostName,
            Port = _fixture.Port,
            UserName = _fixture.UserName,
            Password = _fixture.Password,
            VirtualHost = "/",
            ExchangeName = "test-game-events"
        });

        var factory = new ConnectionFactory
        {
            HostName = _fixture.HostName,
            Port = _fixture.Port,
            UserName = _fixture.UserName,
            Password = _fixture.Password,
            VirtualHost = "/",
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
            RequestedHeartbeat = TimeSpan.FromSeconds(60),
            Ssl = { Enabled = false }
        };

        return new RabbitMQEventPublisher(settings, factory, _logger);
    }

    [Fact]
    public async Task PublishMatchEndedAsync_ShouldPublishEventToRabbitMQ()
    {
        // Arrange
        using var publisher = CreatePublisher();
        var matchId = Guid.NewGuid();
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var winnerId = Guid.NewGuid();
        var loserId = Guid.NewGuid();

        var @event = new GameMatchEndedEvent(
            MatchId: matchId,
            PlayerAId: playerAId,
            PlayerBId: playerBId,
            WinnerId: winnerId,
            LoserId: loserId,
            PlayerATotalPoints: 15000,
            PlayerBTotalPoints: 12000,
            PointsEarned: 24,
            PointsLost: 0,
            StartTime: DateTime.UtcNow.AddMinutes(-10),
            EndTime: DateTime.UtcNow,
            Rounds: new List<GameRoundDto>()
        );

        // Act
        await publisher.PublishMatchEndedAsync(@event);

        // Assert - verify message was published
        await Task.Delay(100); // Give time for message to be published

        // Verify by checking logs
        _logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Event published to RabbitMQ") && o.ToString()!.Contains("match.ended")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task PublishRoundEndedAsync_ShouldPublishEventToRabbitMQ()
    {
        // Arrange
        using var publisher = CreatePublisher();
        var roundId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();

        var @event = new GameRoundEndedEvent(
            MatchId: matchId,
            RoundId: roundId,
            RoundNumber: 1,
            PlayerAId: playerAId,
            PlayerBId: playerBId,
            GameResponse: new CoordinateDto(X: -23.5505, Y: -46.6333),
            PlayerAGuess: new CoordinateDto(X: -23.5500, Y: -46.6330),
            PlayerBGuess: new CoordinateDto(X: -23.5510, Y: -46.6340),
            PlayerAPoints: 4950,
            PlayerBPoints: 4920,
            WinnerId: playerAId,
            EndTime: DateTime.UtcNow
        );

        // Act
        await publisher.PublishRoundEndedAsync(@event);

        // Assert
        await Task.Delay(100);

        _logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Event published to RabbitMQ") && o.ToString()!.Contains("round.ended")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task PublishMatchEndedAsync_WhenCalledMultipleTimes_ShouldReuseConnection()
    {
        // Arrange
        using var publisher = CreatePublisher();
        var matchId1 = Guid.NewGuid();
        var matchId2 = Guid.NewGuid();
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var winnerId = Guid.NewGuid();
        var loserId = Guid.NewGuid();

        var event1 = new GameMatchEndedEvent(matchId1, playerAId, playerBId, winnerId, loserId, 15000, 12000, 24, 0, DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow, new List<GameRoundDto>());
        var event2 = new GameMatchEndedEvent(matchId2, playerAId, playerBId, winnerId, loserId, 14000, 13000, 12, 0, DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow, new List<GameRoundDto>());

        // Act
        await publisher.PublishMatchEndedAsync(event1);
        await publisher.PublishMatchEndedAsync(event2);

        // Assert
        await Task.Delay(100);

        // Verify connection established only once
        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Successfully connected to RabbitMQ")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task Dispose_ShouldCloseConnectionGracefully()
    {
        // Arrange
        var publisher = CreatePublisher();
        var matchId = Guid.NewGuid();
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var @event = new GameMatchEndedEvent(matchId, playerAId, playerBId, Guid.NewGuid(), Guid.NewGuid(), 15000, 12000, 24, 0, DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow, new List<GameRoundDto>());

        await publisher.PublishMatchEndedAsync(@event);

        // Act
        publisher.Dispose();

        // Assert
        _logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("RabbitMQ connection disposed successfully")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task PublishMatchEndedAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var publisher = CreatePublisher();
        publisher.Dispose();

        var @event = new GameMatchEndedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 15000, 12000, 24, 0, DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow, new List<GameRoundDto>());

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await publisher.PublishMatchEndedAsync(@event)
        );
    }

    [Fact]
    public async Task PublishMatchEndedAsync_WithInvalidConnection_ShouldThrowInvalidOperationException()
    {
        // Arrange - create publisher with invalid settings
        var settings = Options.Create(new RabbitMQSettings
        {
            HostName = "invalid-host-12345",
            Port = 9999,
            UserName = "invalid",
            Password = "invalid",
            VirtualHost = "/",
            ExchangeName = "test-game-events"
        });

        var factory = new ConnectionFactory
        {
            HostName = "invalid-host-12345",
            Port = 9999,
            UserName = "invalid",
            Password = "invalid",
            VirtualHost = "/",
            AutomaticRecoveryEnabled = true,
            Ssl = { Enabled = false }
        };

        using var publisher = new RabbitMQEventPublisher(settings, factory, _logger);
        var @event = new GameMatchEndedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 15000, 12000, 24, 0, DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow, new List<GameRoundDto>());

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await publisher.PublishMatchEndedAsync(@event)
        );
    }

}
