using Location404.Game.Application.Features.GameRounds.Interfaces;
using LiteBus.Commands.Abstractions;
using Location404.Game.Application.Common.Interfaces;
using Location404.Game.Application.Common.Result;

using Location404.Game.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Location404.Game.Application.Features.GameRounds.Commands.StartRoundCommand;

public class StartRoundCommandHandler(
    IGameMatchManager matchManager,
    IGuessStorageManager guessStorage,
    IRoundTimerService roundTimer,
    IGeoDataClient geoDataClient,
    ILogger<StartRoundCommandHandler> logger
) : ICommandHandler<StartRoundCommand, Result<StartRoundCommandResponse>>
{
    public async Task<Result<StartRoundCommandResponse>> HandleAsync(
        StartRoundCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Starting round for match {MatchId}", command.MatchId);

            var match = await matchManager.GetMatchAsync(command.MatchId);

            if (match == null)
            {
                logger.LogWarning("Match {MatchId} not found", command.MatchId);
                return Result<StartRoundCommandResponse>.Failure(
                    new Error("Match.NotFound", "Match not found.", ErrorType.NotFound));
            }

            if (!match.CanStartNewRound())
            {
                logger.LogWarning("Cannot start new round for match {MatchId}. Current round count: {RoundCount}",
                    command.MatchId, match.GameRounds?.Count ?? 0);
                return Result<StartRoundCommandResponse>.Failure(
                    new Error("Round.CannotStart", "Match has already reached maximum rounds.", ErrorType.Validation));
            }

            match.StartNewGameRound();
            await matchManager.UpdateMatchAsync(match);

            var locationDto = await geoDataClient.GetRandomLocationAsync();

            Coordinate location;
            int? heading;
            int? pitch;

            if (locationDto != null)
            {
                location = new Coordinate(locationDto.Coordinate.X, locationDto.Coordinate.Y);
                heading = locationDto.Heading ?? Random.Shared.Next(0, 360);
                pitch = locationDto.Pitch ?? Random.Shared.Next(-10, 10);

                logger.LogInformation("Using location from geo-data-service: {Name}", locationDto.Name);
            }
            else
            {
                logger.LogWarning("geo-data-service unavailable, using fallback hardcoded location");
                location = GenerateFallbackLocation();
                heading = Random.Shared.Next(0, 360);
                pitch = Random.Shared.Next(-10, 10);
            }

            await guessStorage.StoreCorrectAnswerAsync(
                match.Id,
                match.CurrentGameRound!.Id,
                location
            );

            var startedAt = DateTimeOffset.UtcNow;
            var durationSeconds = 90;

            logger.LogInformation("Round {RoundNumber} started for match {MatchId} at location ({X}, {Y})",
                match.CurrentGameRound.RoundNumber, command.MatchId, location.X, location.Y);

            await roundTimer.StartTimerAsync(match.Id, match.CurrentGameRound!.Id, TimeSpan.FromSeconds(durationSeconds));

            return Result<StartRoundCommandResponse>.Success(new StartRoundCommandResponse(
                MatchId: match.Id,
                RoundId: match.CurrentGameRound!.Id,
                RoundNumber: match.CurrentGameRound.RoundNumber,
                Location: location,
                Heading: heading,
                Pitch: pitch,
                StartedAt: startedAt,
                DurationSeconds: durationSeconds
            ));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting round for match {MatchId}", command.MatchId);
            return Result<StartRoundCommandResponse>.Failure(
                new Error("StartRound.Failed", $"Error starting round: {ex.Message}", ErrorType.Failure));
        }
    }

    private static Coordinate GenerateFallbackLocation()
    {
        var fallbackLocations = new[]
        {
            new Coordinate(-23.550520, -46.633308),
            new Coordinate(-22.906847, -43.172897),
            new Coordinate(-19.916681, -43.934493),
            new Coordinate(-15.826691, -47.921822),
            new Coordinate(-30.034647, -51.217658)
        };

        return fallbackLocations[Random.Shared.Next(fallbackLocations.Length)];
    }
}
