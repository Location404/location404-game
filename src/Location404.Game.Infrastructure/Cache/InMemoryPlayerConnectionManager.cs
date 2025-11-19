namespace Location404.Game.Infrastructure.Cache;

using Location404.Game.Application.Common.Interfaces;
using Location404.Game.Application.Features.GameRounds.Interfaces;
using Location404.Game.Application.Features.Matchmaking.Interfaces;
using System.Collections.Concurrent;

public class InMemoryPlayerConnectionManager : IPlayerConnectionManager
{
    private readonly ConcurrentDictionary<Guid, string> _connections = new();

    public Task MapPlayerToConnectionAsync(Guid playerId, string connectionId)
    {
        _connections[playerId] = connectionId;
        return Task.CompletedTask;
    }

    public Task<string?> GetConnectionIdAsync(Guid playerId)
    {
        _connections.TryGetValue(playerId, out var connectionId);
        return Task.FromResult(connectionId);
    }

    public Task RemoveMappingAsync(Guid playerId)
    {
        _connections.TryRemove(playerId, out _);
        return Task.CompletedTask;
    }
}
