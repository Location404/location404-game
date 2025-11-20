using Location404.Game.Application.Features.GameRounds.Interfaces;
using Location404.Game.Application.Features.GameRounds.Commands.SubmitGuessCommand;
using LiteBus.Commands.Abstractions;
using Location404.Game.Application.Common.Interfaces;
using Location404.Game.Application.Common.Result;
using Location404.Game.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Location404.Game.Application.Features.GameRounds.Commands.EndRoundCommand;

public class EndRoundCommandHandler(
    IGameMatchManager matchManager,
    IGuessStorageManager guessStorage,
    IRoundTimerService roundTimer,
    IGameEventPublisher eventPublisher,
    ILocation404DataClient location404DataClient,
    ILogger<EndRoundCommandHandler> logger
) : ICommandHandler<EndRoundCommand, Result<EndRoundCommandResponse>>
{
    public async Task<Result<EndRoundCommandResponse>> HandleAsync(
        EndRoundCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await roundTimer.CancelTimerAsync(command.MatchId, command.RoundId);

            logger.LogInformation("✅ [EndRoundHandler] Finalizando rodada {RoundId} para match {MatchId}",
                command.RoundId, command.MatchId);
            logger.LogInformation("📍 [EndRoundHandler] PlayerA Guess: X={PlayerAX} (Lat), Y={PlayerAY} (Lng)",
                command.PlayerAGuess.X, command.PlayerAGuess.Y);
            logger.LogInformation("📍 [EndRoundHandler] PlayerB Guess: X={PlayerBX} (Lat), Y={PlayerBY} (Lng)",
                command.PlayerBGuess.X, command.PlayerBGuess.Y);

            var match = await matchManager.GetMatchAsync(command.MatchId);

            if (match == null)
            {
                logger.LogWarning("Match {MatchId} not found when trying to end round", command.MatchId);
                return Result<EndRoundCommandResponse>.Failure(
                    new Error("Match.NotFound", "Match not found when ending round.", ErrorType.NotFound));
            }

            if (match.CurrentGameRound == null || match.CurrentGameRound.Id != command.RoundId)
            {
                logger.LogInformation("Round {RoundId} was already ended for match {MatchId}. Skipping duplicate end.",
                    command.RoundId, command.MatchId);
                return Result<EndRoundCommandResponse>.Success(
                    new EndRoundCommandResponse(RoundEnded: true, MatchEnded: false));
            }

            var gameResponse = await guessStorage.GetCorrectAnswerAsync(command.MatchId, command.RoundId);

            if (gameResponse == null)
            {
                logger.LogError("❌ [EndRoundHandler] Resposta correta não encontrada para match {MatchId}, round {RoundId}",
                    command.MatchId, command.RoundId);
                return Result<EndRoundCommandResponse>.Failure(
                    new Error("Round.AnswerNotFound", "Round data corrupted.", ErrorType.NotFound));
            }

            logger.LogInformation("🎯 [EndRoundHandler] Resposta Correta: X={CorrectX} (Lat), Y={CorrectY} (Lng)",
                gameResponse.X, gameResponse.Y);

            match.EndCurrentGameRound(gameResponse, command.PlayerAGuess, command.PlayerBGuess);
            await matchManager.UpdateMatchAsync(match);

            var lastRound = match.GameRounds?.Last();
            if (lastRound != null)
            {
                logger.LogInformation("🏆 [EndRoundHandler] Rodada finalizada - PlayerA: {PlayerAPoints} pts, PlayerB: {PlayerBPoints} pts",
                    lastRound.PlayerAPoints, lastRound.PlayerBPoints);
            }

            await guessStorage.ClearGuessesAsync(command.MatchId, command.RoundId);

            if (match.GameRounds == null || !match.GameRounds.Any())
            {
                return Result<EndRoundCommandResponse>.Failure(
                    new Error("Match.InvalidState", "Match must have rounds after ending a round.", ErrorType.Validation));
            }

            var endedRound = match.GameRounds.Last();
            var roundResult = new RoundEndResult(
                RoundId: endedRound.Id,
                RoundNumber: match.GameRounds.Count,
                CorrectLocation: gameResponse,
                PlayerA: new PlayerGuessResult(
                    PlayerId: match.PlayerAId,
                    Guess: command.PlayerAGuess,
                    Points: endedRound.PlayerAPoints ?? 0,
                    DistanceInKm: gameResponse.CalculateDistance(command.PlayerAGuess)
                ),
                PlayerB: new PlayerGuessResult(
                    PlayerId: match.PlayerBId,
                    Guess: command.PlayerBGuess,
                    Points: endedRound.PlayerBPoints ?? 0,
                    DistanceInKm: gameResponse.CalculateDistance(command.PlayerBGuess)
                ),
                PlayerATotalPoints: match.PlayerATotalPoints ?? 0,
                PlayerBTotalPoints: match.PlayerBTotalPoints ?? 0
            );

            try
            {
                var roundEvent = Application.Events.GameRoundEndedEvent.FromGameRound(endedRound);
                await eventPublisher.PublishRoundEndedAsync(roundEvent);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish RoundEnded event for match {MatchId}. Continuing anyway.", command.MatchId);
            }

            if (!match.CanStartNewRound())
            {
                logger.LogInformation("Match {MatchId} is complete. Ending match.", command.MatchId);

                match.EndGameMatch();
                await matchManager.UpdateMatchAsync(match);

                var matchResult = new MatchEndResult(
                    MatchId: match.Id,
                    WinnerId: match.PlayerWinnerId ?? Guid.Empty,
                    LoserId: match.PlayerLoserId ?? Guid.Empty,
                    PlayerAFinalPoints: match.PlayerATotalPoints ?? 0,
                    PlayerBFinalPoints: match.PlayerBTotalPoints ?? 0,
                    PointsEarned: match.PointsEarned ?? 0,
                    PointsLost: match.PointsLost ?? 0,
                    Rounds: match.GameRounds?.ToList() ?? new List<Domain.Entities.GameRound>()
                );

                logger.LogInformation("Match {MatchId} ended. Winner: {WinnerId}",
                    match.Id, match.PlayerWinnerId);

                var matchEvent = Application.Events.GameMatchEndedEvent.FromGameMatch(match);

                try
                {
                    await eventPublisher.PublishMatchEndedAsync(matchEvent);
                    logger.LogInformation("Match ended event published to RabbitMQ for match {MatchId}", match.Id);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to publish MatchEnded event to RabbitMQ for match {MatchId}. Trying HTTP fallback...", match.Id);

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var success = await location404DataClient.SendMatchEndedAsync(matchEvent);
                            if (!success)
                            {
                                logger.LogError("HTTP fallback also failed for match {MatchId}. Match data may not be persisted!", match.Id);
                            }
                        }
                        catch (Exception httpEx)
                        {
                            logger.LogError(httpEx, "HTTP fallback threw exception for match {MatchId}", match.Id);
                        }
                    });
                }

                logger.LogInformation("Removing match {MatchId} from cache...", match.Id);
                await matchManager.RemoveMatchAsync(match.Id);
                logger.LogInformation("Match {MatchId} removed successfully from cache", match.Id);

                return Result<EndRoundCommandResponse>.Success(
                    new EndRoundCommandResponse(RoundEnded: true, MatchEnded: true, roundResult, matchResult));
            }

            return Result<EndRoundCommandResponse>.Success(
                new EndRoundCommandResponse(RoundEnded: true, MatchEnded: false, roundResult));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error ending round for match {MatchId}", command.MatchId);
            return Result<EndRoundCommandResponse>.Failure(
                new Error("EndRound.Failed", $"Error ending round: {ex.Message}", ErrorType.Failure));
        }
    }
}
