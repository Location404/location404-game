using LiteBus.Commands.Abstractions;
using Location404.Game.Application.Common.Result;
using Location404.Game.Domain.Entities;

namespace Location404.Game.Application.Features.Matchmaking.Commands;

public class JoinMatchmakingCommand(Guid PlayerId) : ICommand<Result<JoinMatchmakingResponse>>
{
    public Guid PlayerId { get; } = PlayerId;
}

public record JoinMatchmakingResponse(
    bool MatchFound,
    GameMatch? Match = null
);
