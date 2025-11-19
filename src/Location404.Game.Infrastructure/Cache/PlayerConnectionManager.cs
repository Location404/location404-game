    namespace Location404.Game.Infrastructure.Cache;

using Location404.Game.Application.Common.Interfaces;
using Location404.Game.Application.Features.GameRounds.Interfaces;
using Location404.Game.Application.Features.Matchmaking.Interfaces;
using StackExchange.Redis;

public class PlayerConnectionManager(IConnectionMultiplexer redis) : IPlayerConnectionManager
{
    private readonly IDatabase _db = redis.GetDatabase();
    private const string ConnectionKeyPrefix = "player:connection:";

    public async Task MapPlayerToConnectionAsync(Guid playerId, string connectionId)
    {
        await _db.StringSetAsync(
            $"{ConnectionKeyPrefix}{playerId}", 
            connectionId, 
            TimeSpan.FromHours(24)
        );
    }

    public async Task<string?> GetConnectionIdAsync(Guid playerId)
    {
        var connectionId = await _db.StringGetAsync($"{ConnectionKeyPrefix}{playerId}");
        return connectionId.HasValue ? connectionId.ToString() : null;
    }

    public async Task RemoveMappingAsync(Guid playerId)
    {
        await _db.KeyDeleteAsync($"{ConnectionKeyPrefix}{playerId}");
    }
}