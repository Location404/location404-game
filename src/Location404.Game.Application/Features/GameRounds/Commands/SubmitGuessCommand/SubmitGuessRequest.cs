using Location404.Game.Domain.Entities;

namespace Location404.Game.Application.Features.GameRounds.Commands.SubmitGuessCommand;

public record SubmitGuessRequest(Guid MatchId, Guid PlayerId, double X, double Y)
{
    public Coordinate ToCoordinate() => new(X, Y);
}