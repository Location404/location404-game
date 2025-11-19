namespace Location404.Game.Infrastructure.Matchmaking;

using Location404.Game.Application.Common.Interfaces;
using Location404.Game.Application.Features.GameRounds.Interfaces;
using Location404.Game.Application.Features.Matchmaking.Interfaces;
using Location404.Game.Domain.Entities;
using System.Collections.Concurrent;

/// <summary>
/// In-memory implementation of IMatchmakingService for development without Redis
/// </summary>
public class InMemoryMatchmakingService : IMatchmakingService
{
    private readonly IGameMatchManager _matchManager;
    private readonly SemaphoreSlim _matchmakingLock = new(1, 1);

    // Queue sorted by timestamp, with PlayerId as tiebreaker for uniqueness
    private readonly SortedSet<(Guid PlayerId, long Timestamp)> _queue = new(
        Comparer<(Guid, long)>.Create((a, b) =>
        {
            var timestampComparison = a.Item2.CompareTo(b.Item2);
            if (timestampComparison != 0)
                return timestampComparison;

            // If timestamps are equal, use PlayerId as tiebreaker to ensure uniqueness
            return a.Item1.CompareTo(b.Item1);
        })
    );

    // Set for fast lookup
    private readonly ConcurrentDictionary<Guid, long> _playerTimestamps = new();

    // Counter to guarantee uniqueness when timestamps collide
    private long _timestampCounter = 0;

    public InMemoryMatchmakingService(IGameMatchManager matchManager)
    {
        _matchManager = matchManager ?? throw new ArgumentNullException(nameof(matchManager));
    }

    public async Task<Guid> JoinQueueAsync(Guid playerId)
    {
        if (await _matchManager.IsPlayerInMatchAsync(playerId))
            throw new InvalidOperationException("Player is already in a match.");

        if (await IsPlayerInQueueAsync(playerId))
            throw new InvalidOperationException("Player is already in queue.");

        await _matchmakingLock.WaitAsync();
        try
        {
            // Use high-resolution timestamp with counter to guarantee FIFO order
            var baseTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var timestamp = baseTimestamp * 1000 + Interlocked.Increment(ref _timestampCounter) % 1000;

            _queue.Add((playerId, timestamp));
            _playerTimestamps[playerId] = timestamp;
        }
        finally
        {
            _matchmakingLock.Release();
        }

        return playerId;
    }

    public async Task LeaveQueueAsync(Guid playerId)
    {
        await _matchmakingLock.WaitAsync();
        try
        {
            if (_playerTimestamps.TryRemove(playerId, out var timestamp))
            {
                _queue.Remove((playerId, timestamp));
            }
        }
        finally
        {
            _matchmakingLock.Release();
        }
    }

    public async Task<GameMatch?> TryFindMatchAsync()
    {
        await _matchmakingLock.WaitAsync();

        try
        {
            if (_queue.Count < 2)
                return null;

            var players = _queue.Take(2).ToList();
            var playerAId = players[0].PlayerId;
            var playerBId = players[1].PlayerId;

            // Remove from queue
            _queue.Remove(players[0]);
            _queue.Remove(players[1]);
            _playerTimestamps.TryRemove(playerAId, out _);
            _playerTimestamps.TryRemove(playerBId, out _);

            var match = await _matchManager.CreateMatchAsync(playerAId, playerBId);

            return match;
        }
        finally
        {
            _matchmakingLock.Release();
        }
    }

    public Task<int> GetQueueSizeAsync()
    {
        return Task.FromResult(_queue.Count);
    }

    public Task<bool> IsPlayerInQueueAsync(Guid playerId)
    {
        return Task.FromResult(_playerTimestamps.ContainsKey(playerId));
    }
}
