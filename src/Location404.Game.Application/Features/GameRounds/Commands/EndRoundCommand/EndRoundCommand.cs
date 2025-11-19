using LiteBus.Commands.Abstractions;
using Location404.Game.Application.Common.Result;
using Location404.Game.Domain.Entities;

namespace Location404.Game.Application.Features.GameRounds.Commands.EndRoundCommand;

public class EndRoundCommand(
    Guid MatchId,
    Guid RoundId,
    Coordinate PlayerAGuess,
    Coordinate PlayerBGuess
) : ICommand<Result<EndRoundCommandResponse>>
{
    public Guid MatchId { get; } = MatchId;
    public Guid RoundId { get; } = RoundId;
    public Coordinate PlayerAGuess { get; } = PlayerAGuess;
    public Coordinate PlayerBGuess { get; } = PlayerBGuess;
}
