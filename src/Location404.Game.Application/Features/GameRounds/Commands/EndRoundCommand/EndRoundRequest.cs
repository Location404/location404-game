using Location404.Game.Domain.Entities;

namespace Location404.Game.Application.Features.GameRounds.Commands.EndRoundCommand;

public record EndRoundRequest(
    Guid MatchId,
    double ResponseX,
    double ResponseY,
    Guid PlayerAId,
    double PlayerAGuessX,
    double PlayerAGuessY,
    Guid PlayerBId,
    double PlayerBGuessX,
    double PlayerBGuessY
)
{
    public Coordinate GetGameResponse() => new(ResponseX, ResponseY);
    public Coordinate GetPlayerAGuess() => new(PlayerAGuessX, PlayerAGuessY);
    public Coordinate GetPlayerBGuess() => new(PlayerBGuessX, PlayerBGuessY);
}