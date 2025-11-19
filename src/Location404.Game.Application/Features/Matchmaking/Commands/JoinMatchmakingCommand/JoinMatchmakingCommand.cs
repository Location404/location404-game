using LiteBus.Commands.Abstractions;
using Location404.Game.Application.Common.Result;

namespace Location404.Game.Application.Features.Matchmaking.Commands.JoinMatchmakingCommand;

public class JoinMatchmakingCommand(Guid PlayerId) : ICommand<Result<JoinMatchmakingCommandResponse>>
{
    public Guid PlayerId { get; } = PlayerId;
}
