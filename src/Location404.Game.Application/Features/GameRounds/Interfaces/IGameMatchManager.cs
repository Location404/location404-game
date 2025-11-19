using Location404.Game.Domain.Entities;

namespace Location404.Game.Application.Features.GameRounds.Interfaces;

public interface IGameMatchManager
{
    Task<GameMatch> CreateMatchAsync(Guid playerAId, Guid playerBId);
    Task<GameMatch?> GetMatchAsync(Guid matchId);
    Task<GameMatch?> GetPlayerCurrentMatchAsync(Guid playerId);
    Task UpdateMatchAsync(GameMatch match);
    Task RemoveMatchAsync(Guid matchId);
    Task<bool> IsPlayerInMatchAsync(Guid playerId);
    Task<IEnumerable<Guid>> GetAllActiveMatchIdsAsync();
    Task ClearPlayerMatchStateAsync(Guid playerId);
}