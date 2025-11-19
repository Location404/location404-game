using Location404.Game.Application.Features.GameRounds.Commands.SubmitGuessCommand;

namespace Location404.Game.Application.Features.GameRounds.Commands.EndRoundCommand;

public record EndRoundCommandResponse(
    bool RoundEnded,
    bool MatchEnded,
    RoundEndResult? RoundResult = null,
    MatchEndResult? MatchResult = null
);
