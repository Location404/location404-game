using Location404.Game.Domain.Entities;

namespace Location404.Game.Application.DTOs.Responses;

public record RoundEndedResponse(
    Guid MatchId,
    Guid RoundId,
    int RoundNumber,
    CoordinateDto CorrectAnswer,
    CoordinateDto? PlayerAGuess,
    CoordinateDto? PlayerBGuess,
    int? PlayerAPoints,
    int? PlayerBPoints,
    int? PlayerATotalPoints,
    int? PlayerBTotalPoints,
    Guid? RoundWinnerId
)
{
    public static RoundEndedResponse FromGameRound(GameRound round, int? playerATotalPoints, int? playerBTotalPoints)
    {
        if (round.GameResponse == null)
            throw new InvalidOperationException("Round must be ended before creating response.");

        return new RoundEndedResponse(
            round.GameMatchId,
            round.Id,
            round.RoundNumber,
            CoordinateDto.FromEntity(round.GameResponse),
            round.PlayerAGuess != null ? CoordinateDto.FromEntity(round.PlayerAGuess) : null,
            round.PlayerBGuess != null ? CoordinateDto.FromEntity(round.PlayerBGuess) : null,
            round.PlayerAPoints ?? 0,
            round.PlayerBPoints ?? 0,
            playerATotalPoints,
            playerBTotalPoints,
            round.PlayerWinner()
        );
    }
}