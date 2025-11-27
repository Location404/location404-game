using Location404.Game.Domain.Entities;

namespace Location404.Game.Application.Features.GameRounds.Commands.SubmitGuessCommand;

public record SubmitGuessCommandResponse(
    bool RoundEnded,
    bool MatchEnded,
    Guid PlayerId,
    RoundEndResult? RoundResult = null,
    MatchEndResult? MatchResult = null,
    bool TimerAdjusted = false,
    int? NewTimerDuration = null,
    Guid? RoundId = null
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
    Guid LoserId,
    int PlayerAFinalPoints,
    int PlayerBFinalPoints,
    int PointsEarned,
    int PointsLost,
    List<GameRound> Rounds
);
