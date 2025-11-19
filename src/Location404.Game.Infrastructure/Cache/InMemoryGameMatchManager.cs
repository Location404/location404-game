using Location404.Game.Application.Common.Interfaces;
using Location404.Game.Application.Features.GameRounds.Interfaces;
using Location404.Game.Application.Features.Matchmaking.Interfaces;
using Location404.Game.Domain.Entities;

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Location404.Game.Infrastructure.Cache;

/// <summary>
/// In-memory implementation of IGameMatchManager for development without Redis
/// </summary>
public class InMemoryGameMatchManager : IGameMatchManager
{
    private readonly ILogger<InMemoryGameMatchManager> _logger;
    private readonly ConcurrentDictionary<Guid, GameMatch> _matches = new();
    private readonly ConcurrentDictionary<Guid, Guid> _playerToMatch = new();

    public InMemoryGameMatchManager(ILogger<InMemoryGameMatchManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<GameMatch> CreateMatchAsync(Guid playerAId, Guid playerBId)
    {
        _logger.LogInformation("Creating match for players {PlayerA} and {PlayerB}", playerAId, playerBId);

        var match = GameMatch.StartGameMatch(playerAId, playerBId);

        _matches[match.Id] = match;
        _playerToMatch[playerAId] = match.Id;
        _playerToMatch[playerBId] = match.Id;

        _logger.LogInformation("Match {MatchId} created successfully", match.Id);

        return Task.FromResult(match);
    }

    public Task<GameMatch?> GetMatchAsync(Guid matchId)
    {
        _matches.TryGetValue(matchId, out var match);
        return Task.FromResult(match);
    }

    public Task<GameMatch?> GetPlayerCurrentMatchAsync(Guid playerId)
    {
        if (_playerToMatch.TryGetValue(playerId, out var matchId))
        {
            _matches.TryGetValue(matchId, out var match);
            return Task.FromResult(match);
        }

        return Task.FromResult<GameMatch?>(null);
    }

    public Task UpdateMatchAsync(GameMatch match)
    {
        _matches[match.Id] = match;
        return Task.CompletedTask;
    }

    public Task RemoveMatchAsync(Guid matchId)
    {
        _logger.LogInformation("Removing match {MatchId}", matchId);

        if (_matches.TryRemove(matchId, out var match))
        {
            _playerToMatch.TryRemove(match.PlayerAId, out _);
            _playerToMatch.TryRemove(match.PlayerBId, out _);
            _logger.LogInformation("Match {MatchId} removed successfully", matchId);
        }
        else
        {
            _logger.LogWarning("Match {MatchId} not found during removal", matchId);
        }

        return Task.CompletedTask;
    }

    public Task<bool> IsPlayerInMatchAsync(Guid playerId)
    {
        return Task.FromResult(_playerToMatch.ContainsKey(playerId));
    }

    public Task<IEnumerable<Guid>> GetAllActiveMatchIdsAsync()
    {
        return Task.FromResult<IEnumerable<Guid>>(_matches.Keys.ToList());
    }

    public Task ClearPlayerMatchStateAsync(Guid playerId)
    {
        _logger.LogInformation("Clearing match state for player {PlayerId}", playerId);
        _playerToMatch.TryRemove(playerId, out _);
        _logger.LogInformation("Player {PlayerId} match state cleared", playerId);
        return Task.CompletedTask;
    }
}
