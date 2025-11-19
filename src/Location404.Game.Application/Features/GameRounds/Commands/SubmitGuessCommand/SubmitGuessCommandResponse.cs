using Location404.Game.Domain.Entities;

namespace Location404.Game.Application.Features.GameRounds.Commands.SubmitGuessCommand;

public record SubmitGuessCommandResponse(
    bool RoundEnded,
    bool MatchEnded,
    RoundEndResult? RoundResult = null,
    MatchEndResult? MatchResult = null
);

public record RoundEndResult(
    Guid RoundId,
    int RoundNumber,
    Coordinate CorrectLocation,
    PlayerGuessResult PlayerA,
    PlayerGuessResult PlayerB,
    int PlayerATotalPoints,
    int PlayerBTotalPoints
);

public record PlayerGuessResult(
    Guid PlayerId,
    Coordinate Guess,
    int Points,
    double DistanceInKm
);

public record MatchEndResult(
    Guid MatchId,
    Guid WinnerId,
    int PlayerAFinalPoints,
    int PlayerBFinalPoints
);
