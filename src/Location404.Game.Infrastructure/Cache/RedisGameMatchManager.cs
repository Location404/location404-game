using Location404.Game.Application.Common.Interfaces;
using Location404.Game.Application.Features.GameRounds.Interfaces;
using Location404.Game.Application.Features.Matchmaking.Interfaces;
using Location404.Game.Domain.Entities;

using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Location404.Game.Infrastructure.Cache;

public class RedisGameMatchManager : IGameMatchManager
{
    private readonly IDatabase _db;
    private readonly ILogger<RedisGameMatchManager> _logger;
    private readonly TimeSpan _matchExpiration = TimeSpan.FromHours(2);

    public RedisGameMatchManager(IConnectionMultiplexer redis, ILogger<RedisGameMatchManager> logger)
    {
        _db = redis.GetDatabase();
        _logger = logger;
    }
    
    private const string MatchKeyPrefix = "match:";
    private const string PlayerMatchKeyPrefix = "player:match:";
    private const string ActiveMatchesKey = "matches:active";

    public async Task<GameMatch> CreateMatchAsync(Guid playerAId, Guid playerBId)
    {
        _logger.LogInformation("Creating match for players {PlayerA} and {PlayerB}", playerAId, playerBId);

        var match = GameMatch.StartGameMatch(playerAId, playerBId);

        await SaveMatchAsync(match);

        await _db.StringSetAsync(
            GetPlayerMatchKey(playerAId),
            match.Id.ToString(),
            _matchExpiration
        );

        await _db.StringSetAsync(
            GetPlayerMatchKey(playerBId),
            match.Id.ToString(),
            _matchExpiration
        );

        await _db.SetAddAsync(ActiveMatchesKey, match.Id.ToString());

        _logger.LogInformation("Match {MatchId} created successfully", match.Id);

        return match;
    }

    public async Task<GameMatch?> GetMatchAsync(Guid matchId)
    {
        var key = GetMatchKey(matchId);
        var json = await _db.StringGetAsync(key);

        return json.IsNullOrEmpty 
            ? null 
            : GameMatchSerializer.Deserialize(json!);
    }

    public async Task<GameMatch?> GetPlayerCurrentMatchAsync(Guid playerId)
    {
        var matchIdStr = await _db.StringGetAsync(GetPlayerMatchKey(playerId));
        
        if (matchIdStr.IsNullOrEmpty)
            return null;
        
        if (!Guid.TryParse(matchIdStr, out var matchId))
            return null;
        
        return await GetMatchAsync(matchId);
    }

    public async Task UpdateMatchAsync(GameMatch match)
    {
        var lockKey = $"lock:match:{match.Id}";
        var lockValue = Guid.NewGuid().ToString();
        var lockExpiry = TimeSpan.FromSeconds(10);

        var lockAcquired = await _db.StringSetAsync(lockKey, lockValue, lockExpiry, When.NotExists);

        if (!lockAcquired)
        {
            _logger.LogWarning("Could not acquire lock for match {MatchId}. Retrying...", match.Id);
            await Task.Delay(100);
            lockAcquired = await _db.StringSetAsync(lockKey, lockValue, lockExpiry, When.NotExists);

            if (!lockAcquired)
            {
                throw new InvalidOperationException($"Could not acquire lock for match {match.Id} after retry");
            }
        }

        try
        {
            await SaveMatchAsync(match);
        }
        finally
        {
            var script = @"
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('del', KEYS[1])
                else
                    return 0
                end";

            await _db.ScriptEvaluateAsync(script, new RedisKey[] { lockKey }, new RedisValue[] { lockValue });
        }
    }

    public async Task RemoveMatchAsync(Guid matchId)
    {
        _logger.LogInformation("Removing match {MatchId}", matchId);

        var match = await GetMatchAsync(matchId);
        if (match == null)
        {
            _logger.LogWarning("Match {MatchId} not found during removal", matchId);
            return;
        }

        await _db.KeyDeleteAsync(GetPlayerMatchKey(match.PlayerAId));
        await _db.KeyDeleteAsync(GetPlayerMatchKey(match.PlayerBId));
        await _db.KeyDeleteAsync(GetMatchKey(matchId));
        await _db.SetRemoveAsync(ActiveMatchesKey, matchId.ToString());

        _logger.LogInformation("Match {MatchId} removed successfully", matchId);
    }

    public async Task<bool> IsPlayerInMatchAsync(Guid playerId)
    {
        var matchId = await _db.StringGetAsync(GetPlayerMatchKey(playerId));
        return !matchId.IsNullOrEmpty;
    }

    public async Task<IEnumerable<Guid>> GetAllActiveMatchIdsAsync()
    {
        var matchIds = await _db.SetMembersAsync(ActiveMatchesKey);
        return matchIds
            .Select(id => Guid.TryParse(id, out var guid) ? guid : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value);
    }

    public async Task ClearPlayerMatchStateAsync(Guid playerId)
    {
        _logger.LogInformation("Clearing match state for player {PlayerId}", playerId);
        await _db.KeyDeleteAsync(GetPlayerMatchKey(playerId));
        _logger.LogInformation("Player {PlayerId} match state cleared", playerId);
    }

    private async Task SaveMatchAsync(GameMatch match)
    {
        var key = GetMatchKey(match.Id);
        var json = GameMatchSerializer.Serialize(match);
        await _db.StringSetAsync(key, json, _matchExpiration);
    }

    private static string GetMatchKey(Guid matchId) 
        => $"{MatchKeyPrefix}{matchId}";

    private static string GetPlayerMatchKey(Guid playerId) 
        => $"{PlayerMatchKeyPrefix}{playerId}";
}