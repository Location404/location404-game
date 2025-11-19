namespace Location404.Game.Application.Features.Matchmaking.Commands.JoinMatchmakingCommand;

public record MatchFoundResponse(
    Guid MatchId,
    Guid PlayerAId,
    Guid PlayerBId,
    DateTime StartTime
);