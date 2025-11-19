namespace Location404.Game.Application.Features.GameRounds.Interfaces;

public interface IRoundTimerService
{
    Task StartTimerAsync(Guid matchId, Guid roundId, TimeSpan duration);
    Task CancelTimerAsync(Guid matchId, Guid roundId);
    Task<TimeSpan?> GetRemainingTimeAsync(Guid matchId, Guid roundId);
    Task AdjustTimerAsync(Guid matchId, Guid roundId, TimeSpan newDuration);
}
