using LiteBus.Commands.Abstractions;
using Location404.Game.Application.Common.Result;
using Location404.Game.Domain.Entities;

namespace Location404.Game.Application.Features.GameRounds.Commands;

public class SubmitGuessCommand(
    Guid MatchId,
    Guid PlayerId,
    Coordinate Guess
) : ICommand<Result<SubmitGuessResponse>>
{
    public Guid MatchId { get; } = MatchId;
    public Guid PlayerId { get; } = PlayerId;
    public Coordinate Guess { get; } = Guess;
}

public record SubmitGuessResponse(
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
