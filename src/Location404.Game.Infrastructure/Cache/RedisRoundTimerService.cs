namespace Location404.Game.Infrastructure.Cache;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Location404.Game.Application.Features.GameRounds.Interfaces;

public class RedisRoundTimerService : IRoundTimerService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisRoundTimerService> _logger;
    private const string TimerKeyPrefix = "round:timer";

    public RedisRoundTimerService(
        IConnectionMultiplexer redis,
        ILogger<RedisRoundTimerService> logger)
    {
        _redis = redis;
        _logger = logger;

        EnsureKeyspaceNotificationsEnabled();
    }

    private void EnsureKeyspaceNotificationsEnabled()
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            server.ConfigSet("notify-keyspace-events", "Ex");
            _logger.LogInformation("⏱️ [RedisTimer] Keyspace notifications enabled (Ex)");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⏱️ [RedisTimer] Failed to enable keyspace notifications. Timer may not work correctly.");
        }
    }

    public async Task StartTimerAsync(Guid matchId, Guid roundId, TimeSpan duration)
    {
        var db = _redis.GetDatabase();
        var key = GetTimerKey(matchId, roundId);

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await db.StringSetAsync(key, timestamp, duration);

        _logger.LogInformation("⏱️ [RedisTimer] Started {Duration}s timer for match {MatchId}, round {RoundId}",
            duration.TotalSeconds, matchId, roundId);
    }

    public async Task CancelTimerAsync(Guid matchId, Guid roundId)
    {
        var db = _redis.GetDatabase();
        var key = GetTimerKey(matchId, roundId);

        var deleted = await db.KeyDeleteAsync(key);

        if (deleted)
        {
            _logger.LogInformation("⏱️ [RedisTimer] Cancelled timer for match {MatchId}, round {RoundId}",
                matchId, roundId);
        }
    }

    public async Task<TimeSpan?> GetRemainingTimeAsync(Guid matchId, Guid roundId)
    {
        var db = _redis.GetDatabase();
        var key = GetTimerKey(matchId, roundId);

        var ttl = await db.KeyTimeToLiveAsync(key);

        if (ttl.HasValue)
        {
            _logger.LogDebug("⏱️ [RedisTimer] Remaining time for match {MatchId}, round {RoundId}: {Remaining}s",
                matchId, roundId, ttl.Value.TotalSeconds);
        }

        return ttl;
    }

    public async Task AdjustTimerAsync(Guid matchId, Guid roundId, TimeSpan newDuration)
    {
        var db = _redis.GetDatabase();
        var key = GetTimerKey(matchId, roundId);

        var exists = await db.KeyExistsAsync(key);
        if (!exists)
        {
            _logger.LogWarning("⏱️ [RedisTimer] Cannot adjust timer - key does not exist for match {MatchId}, round {RoundId}",
                matchId, roundId);
            return;
        }

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await db.StringSetAsync(key, timestamp, newDuration);

        _logger.LogInformation("⏱️ [RedisTimer] Adjusted timer to {Duration}s for match {MatchId}, round {RoundId}",
            newDuration.TotalSeconds, matchId, roundId);
    }

    private static string GetTimerKey(Guid matchId, Guid roundId)
        => $"{TimerKeyPrefix}:{matchId}:{roundId}";
}
