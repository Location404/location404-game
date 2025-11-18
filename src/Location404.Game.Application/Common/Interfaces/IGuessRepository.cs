using Location404.Game.Domain.Entities;

namespace Location404.Game.Application.Common.Interfaces;

public interface IGuessRepository
{
    Task SaveGuessAsync(Guid matchId, Guid roundId, Guid playerId, Coordinate guess, CancellationToken cancellationToken = default);
    Task<Coordinate?> GetGuessAsync(Guid matchId, Guid roundId, Guid playerId, CancellationToken cancellationToken = default);
    Task<bool> HasPlayerSubmittedAsync(Guid matchId, Guid roundId, Guid playerId, CancellationToken cancellationToken = default);
    Task DeleteGuessesForRoundAsync(Guid matchId, Guid roundId, CancellationToken cancellationToken = default);
}
