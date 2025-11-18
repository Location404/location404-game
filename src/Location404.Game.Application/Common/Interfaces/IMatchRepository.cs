using Location404.Game.Domain.Entities;

namespace Location404.Game.Application.Common.Interfaces;

public interface IMatchRepository
{
    Task<GameMatch?> GetByIdAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task<GameMatch?> GetByPlayerIdAsync(Guid playerId, CancellationToken cancellationToken = default);
    Task SaveAsync(GameMatch match, CancellationToken cancellationToken = default);
    Task UpdateAsync(GameMatch match, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid matchId, CancellationToken cancellationToken = default);
}
