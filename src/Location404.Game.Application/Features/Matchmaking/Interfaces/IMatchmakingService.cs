using Location404.Game.Domain.Entities;

namespace Location404.Game.Application.Features.Matchmaking.Interfaces;

public interface IMatchmakingService
{
    Task<Guid> JoinQueueAsync(Guid playerId);
    Task LeaveQueueAsync(Guid playerId);
    Task<GameMatch?> TryFindMatchAsync();
    Task<int> GetQueueSizeAsync();
    Task<bool> IsPlayerInQueueAsync(Guid playerId);
}