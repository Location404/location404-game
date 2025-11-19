using Location404.Game.Domain.Entities;

namespace Location404.Game.Application.Features.Matchmaking.Commands.JoinMatchmakingCommand;

public record JoinMatchmakingCommandResponse(
    bool MatchFound,
    GameMatch? Match = null
);
