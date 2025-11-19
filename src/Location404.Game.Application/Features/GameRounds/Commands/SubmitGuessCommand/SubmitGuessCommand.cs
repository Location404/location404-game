using LiteBus.Commands.Abstractions;
using Location404.Game.Application.Common.Result;
using Location404.Game.Domain.Entities;

namespace Location404.Game.Application.Features.GameRounds.Commands.SubmitGuessCommand;

public class SubmitGuessCommand(
    Guid MatchId,
    Guid PlayerId,
    Coordinate Guess
) : ICommand<Result<SubmitGuessCommandResponse>>
{
    public Guid MatchId { get; } = MatchId;
    public Guid PlayerId { get; } = PlayerId;
    public Coordinate Guess { get; } = Guess;
}
