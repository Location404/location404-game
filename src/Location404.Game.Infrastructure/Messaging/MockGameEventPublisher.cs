namespace Location404.Game.Infrastructure.Messaging;

using Location404.Game.Application.Events;
using Location404.Game.Application.Common.Interfaces;
using Location404.Game.Application.Features.GameRounds.Interfaces;
using Location404.Game.Application.Features.Matchmaking.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

/// <summary>
/// Mock implementation of IGameEventPublisher for development without RabbitMQ
/// Logs events instead of publishing to message broker
/// </summary>
public class MockGameEventPublisher : IGameEventPublisher
{
    private readonly ILogger<MockGameEventPublisher> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public MockGameEventPublisher(ILogger<MockGameEventPublisher> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    public Task PublishMatchEndedAsync(GameMatchEndedEvent @event)
    {
        var json = JsonSerializer.Serialize(@event, _jsonOptions);

        _logger.LogInformation(
            "[MOCK] Match Ended Event - MatchId: {MatchId}\n{EventJson}",
            @event.MatchId,
            json
        );

        return Task.CompletedTask;
    }

    public Task PublishRoundEndedAsync(GameRoundEndedEvent @event)
    {
        var json = JsonSerializer.Serialize(@event, _jsonOptions);

        _logger.LogInformation(
            "[MOCK] Round Ended Event - MatchId: {MatchId}, Round: {RoundNumber}\n{EventJson}",
            @event.MatchId,
            @event.RoundNumber,
            json
        );

        return Task.CompletedTask;
    }
}
