namespace Location404.Game.Infrastructure.Matchmaking;

using Location404.Game.Application.Common.Interfaces;
using Location404.Game.Application.Features.GameRounds.Interfaces;
using Location404.Game.Application.Features.Matchmaking.Interfaces;
using Location404.Game.Domain.Entities;

using Microsoft.Extensions.Logging;
using StackExchange.Redis;

public class RedisMatchmakingService(IConnectionMultiplexer redis, IGameMatchManager matchManager, ILogger<RedisMatchmakingService> logger) : IMatchmakingService
{
    private readonly IConnectionMultiplexer _redis = redis;
    private readonly IDatabase _db = redis.GetDatabase();
    private readonly IGameMatchManager _matchManager = matchManager;
    private readonly ILogger<RedisMatchmakingService> _logger = logger;
    private readonly SemaphoreSlim _matchmakingLock = new(1, 1);

    private const string QueueKey = "matchmaking:queue";
    private const string PlayerQueueKey = "matchmaking:players";

    public async Task<Guid> JoinQueueAsync(Guid playerId)
    {
        _logger.LogInformation("Player {PlayerId} attempting to join matchmaking queue", playerId);

        if (await _matchManager.IsPlayerInMatchAsync(playerId))
        {
            _logger.LogWarning("Player {PlayerId} rejected - already in match", playerId);
            throw new InvalidOperationException("Player is already in a match.");
        }

        if (await IsPlayerInQueueAsync(playerId))
        {
            _logger.LogWarning("Player {PlayerId} rejected - already in queue", playerId);
            throw new InvalidOperationException("Player is already in queue.");
        }

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await _db.SortedSetAddAsync(QueueKey, playerId.ToString(), timestamp);
        await _db.SetAddAsync(PlayerQueueKey, playerId.ToString());

        var queueSize = await GetQueueSizeAsync();
        _logger.LogInformation("Player {PlayerId} added to queue successfully. Queue size: {QueueSize}", playerId, queueSize);

        return playerId;
    }

    public async Task LeaveQueueAsync(Guid playerId)
    {
        await _db.SortedSetRemoveAsync(QueueKey, playerId.ToString());
        await _db.SetRemoveAsync(PlayerQueueKey, playerId.ToString());
    }

    public async Task<GameMatch?> TryFindMatchAsync()
    {
        _logger.LogInformation("TryFindMatchAsync called - attempting to find match");

        await _matchmakingLock.WaitAsync();


        try
        {
            var queueSize = await GetQueueSizeAsync();
            _logger.LogInformation("Current queue size: {QueueSize}", queueSize);

            var players = await _db.SortedSetRangeByRankAsync(QueueKey, 0, 1);

            _logger.LogInformation("Retrieved {PlayerCount} players from queue", players.Length);

            if (players.Length < 2)
            {
                _logger.LogInformation("Not enough players to create match (need 2, have {Count})", players.Length);
                return null;
            }

            var playerAId = Guid.Parse(players[0]!);
            var playerBId = Guid.Parse(players[1]!);

            _logger.LogInformation("Creating match between Player A: {PlayerA} and Player B: {PlayerB}", playerAId, playerBId);

            await LeaveQueueAsync(playerAId);
            await LeaveQueueAsync(playerBId);

            var match = await _matchManager.CreateMatchAsync(playerAId, playerBId);

            _logger.LogInformation("Match {MatchId} created successfully for players {PlayerA} and {PlayerB}", match.Id, playerAId, playerBId);

            return match;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while trying to find match");
            throw;
        }
        finally
        {
            _matchmakingLock.Release();
        }
    }

    public async Task<int> GetQueueSizeAsync()
    {
        return (int)await _db.SortedSetLengthAsync(QueueKey);
    }

    public async Task<bool> IsPlayerInQueueAsync(Guid playerId)
    {
        return await _db.SetContainsAsync(PlayerQueueKey, playerId.ToString());
    }
}