using LiteBus.Commands.Abstractions;
using Location404.Game.Application.Common.Result;

namespace Location404.Game.Application.Features.GameRounds.Commands.StartRoundCommand;

public class StartRoundCommand(Guid MatchId) : ICommand<Result<StartRoundCommandResponse>>
{
    public Guid MatchId { get; } = MatchId;
}
