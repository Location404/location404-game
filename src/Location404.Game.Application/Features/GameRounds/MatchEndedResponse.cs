using Location404.Game.Domain.Entities;
using Location404.Game.Application.Features.GameRounds.Commands.SubmitGuessCommand;
using Location404.Game.Application.DTOs;

namespace Location404.Game.Application.Features.GameRounds;

public record MatchEndedResponse(
    Guid MatchId,
    Guid? WinnerId,
    Guid? LoserId,
    int? PlayerATotalPoints,
    int? PlayerBTotalPoints,
    int? PointsEarned,
    int? PointsLost,
    DateTime EndTime,
    List<GameRoundDto> Rounds
)
{
    public static MatchEndedResponse FromGameMatch(GameMatch match)
    {
        return match.GameRounds == null
            ? throw new InvalidOperationException("Match must have rounds before creating response.")
            : new MatchEndedResponse(
            match.Id,
            match.PlayerWinnerId,
            match.PlayerLoserId,
            match.PlayerATotalPoints,
            match.PlayerBTotalPoints,
            match.PointsEarned,
            match.PointsLost,
            match.EndTime,
            match.GameRounds.Select(GameRoundDto.FromEntity).ToList()
        );
    }

    public static MatchEndedResponse FromMatchEndResult(MatchEndResult result)
    {
        return new MatchEndedResponse(
            result.MatchId,
            result.WinnerId != Guid.Empty ? result.WinnerId : null,
            result.LoserId != Guid.Empty ? result.LoserId : null,
            result.PlayerAFinalPoints,
            result.PlayerBFinalPoints,
            result.PointsEarned,
            result.PointsLost,
            DateTime.UtcNow,
            result.Rounds.Select(GameRoundDto.FromEntity).ToList()
        );
    }
}