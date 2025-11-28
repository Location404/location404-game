namespace Location404.Game.Infrastructure.Cache;

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Location404.Game.Application.Features.GameRounds.Interfaces;

public class InMemoryRoundTimerService : IRoundTimerService
{
    private readonly ILogger<InMemoryRoundTimerService> _logger;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _timers = new();
    private readonly ConcurrentDictionary<string, DateTime> _timerStartTimes = new();

    public InMemoryRoundTimerService(ILogger<InMemoryRoundTimerService> logger)
    {
        _logger = logger;
    }

    public Task StartTimerAsync(Guid matchId, Guid roundId, TimeSpan duration)
    {
        var key = GetKey(matchId, roundId);
        var cts = new CancellationTokenSource();

        _timers[key] = cts;

        _logger.LogInformation("⏱️ [InMemoryTimer] Started {Duration}s timer for match {MatchId}, round {RoundId}",
            duration.TotalSeconds, matchId, roundId);

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(duration, cts.Token);
                _logger.LogWarning("⏱️ [InMemoryTimer] Timer expired for match {MatchId}, round {RoundId}", matchId, roundId);
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("⏱️ [InMemoryTimer] Timer cancelled for match {MatchId}, round {RoundId}", matchId, roundId);
            }
            finally
            {
                _timers.TryRemove(key, out _);
            }
        }, cts.Token);

        return Task.CompletedTask;
    }

    public Task CancelTimerAsync(Guid matchId, Guid roundId)
    {
        var key = GetKey(matchId, roundId);

        if (_timers.TryRemove(key, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            _timerStartTimes.TryRemove(key, out _);
            _logger.LogInformation("⏱️ [InMemoryTimer] Cancelled timer for match {MatchId}, round {RoundId}", matchId, roundId);
        }

        return Task.CompletedTask;
    }

    public Task<TimeSpan?> GetRemainingTimeAsync(Guid matchId, Guid roundId)
    {
        _logger.LogWarning("⏱️ [InMemoryTimer] GetRemainingTimeAsync not implemented for InMemory - returning null");
        return Task.FromResult<TimeSpan?>(null);
    }

    public Task AdjustTimerAsync(Guid matchId, Guid roundId, TimeSpan newDuration)
    {
        _logger.LogWarning("⏱️ [InMemoryTimer] AdjustTimerAsync not implemented for InMemory - cancelling and restarting");
        CancelTimerAsync(matchId, roundId);
        return StartTimerAsync(matchId, roundId, newDuration);
    }

    private static string GetKey(Guid matchId, Guid roundId) => $"{matchId}:{roundId}";
}
