using LiteBus.Commands.Abstractions;
using Location404.Game.Application.Common.Interfaces;
using Location404.Game.Application.Common.Result;
using Location404.Game.Application.Services;
using Location404.Game.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Location404.Game.Application.Features.GameRounds.Commands;

public class SubmitGuessCommandHandler(
    IMatchRepository matchRepository,
    IGuessRepository guessRepository,
    IGameMatchManager matchManager,
    IGuessStorageManager guessStorage,
    IRoundTimerService roundTimer,
    IGameEventPublisher eventPublisher,
    IGeoDataClient geoDataClient,
    ILogger<SubmitGuessCommandHandler> logger
) : ICommandHandler<SubmitGuessCommand, Result<SubmitGuessResponse>>
{
    public async Task<Result<SubmitGuessResponse>> HandleAsync(
        SubmitGuessCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Player {PlayerId} submitting guess for match {MatchId}",
                command.PlayerId, command.MatchId);

            var match = await matchManager.GetMatchAsync(command.MatchId);

            if (match == null)
            {
                logger.LogWarning("Match {MatchId} not found", command.MatchId);
                return Result<SubmitGuessResponse>.Failure(
                    new Error("Match.NotFound", "Match not found.", ErrorType.NotFound));
            }

            if (match.CurrentGameRound == null)
            {
                logger.LogWarning("No active round for match {MatchId}", command.MatchId);
                return Result<SubmitGuessResponse>.Failure(
                    new Error("Round.NotActive", "No active round.", ErrorType.Validation));
            }

            var currentRoundId = match.CurrentGameRound.Id;

            logger.LogInformation("📥 [Handler] Palpite recebido de {PlayerId}: X={X} (Lat), Y={Y} (Lng)",
                command.PlayerId, command.Guess.X, command.Guess.Y);

            await guessStorage.StoreGuessAsync(
                command.MatchId,
                currentRoundId,
                command.PlayerId,
                command.Guess
            );

            logger.LogInformation("✅ [Handler] Palpite armazenado para player {PlayerId}",
                command.PlayerId);

            var (playerAGuess, playerBGuess) = await guessStorage.GetBothGuessesAsync(
                command.MatchId,
                currentRoundId,
                match.PlayerAId,
                match.PlayerBId
            );

            var isFirstGuess = (playerAGuess != null && playerBGuess == null) ||
                              (playerAGuess == null && playerBGuess != null);

            if (isFirstGuess)
            {
                await HandleFirstGuessSubmittedAsync(command.MatchId, currentRoundId);
            }

            if (playerAGuess != null && playerBGuess != null)
            {
                return await HandleBothGuessesSubmittedAsync(match, currentRoundId, playerAGuess, playerBGuess, cancellationToken);
            }

            return Result<SubmitGuessResponse>.Success(
                new SubmitGuessResponse(RoundEnded: false, MatchEnded: false));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error submitting guess for player {PlayerId} in match {MatchId}",
                command.PlayerId, command.MatchId);
            return Result<SubmitGuessResponse>.Failure(
                new Error("SubmitGuess.Failed", $"Error submitting guess: {ex.Message}", ErrorType.Failure));
        }
    }

    private async Task HandleFirstGuessSubmittedAsync(Guid matchId, Guid roundId)
    {
        var remainingTime = await roundTimer.GetRemainingTimeAsync(matchId, roundId);

        if (remainingTime.HasValue && remainingTime.Value.TotalSeconds > 15)
        {
            logger.LogInformation("⏱️ [Handler] Primeiro palpite detectado. Ajustando timer de {Current}s para 15s",
                remainingTime.Value.TotalSeconds);

            await roundTimer.AdjustTimerAsync(matchId, roundId, TimeSpan.FromSeconds(15));
        }
        else if (remainingTime.HasValue)
        {
            logger.LogInformation("⏱️ [Handler] Primeiro palpite detectado, mas timer já está em {Current}s (≤15s)",
                remainingTime.Value.TotalSeconds);
        }
    }

    private async Task<Result<SubmitGuessResponse>> HandleBothGuessesSubmittedAsync(
        GameMatch match,
        Guid currentRoundId,
        Coordinate playerAGuess,
        Coordinate playerBGuess,
        CancellationToken cancellationToken)
    {
        await roundTimer.CancelTimerAsync(match.Id, currentRoundId);

        logger.LogInformation("✅ [Handler] Ambos jogadores enviaram palpites para match {MatchId}. Finalizando rodada...",
            match.Id);
        logger.LogInformation("📍 [Handler] PlayerA Guess: X={PlayerAX} (Lat), Y={PlayerAY} (Lng)",
            playerAGuess.X, playerAGuess.Y);
        logger.LogInformation("📍 [Handler] PlayerB Guess: X={PlayerBX} (Lat), Y={PlayerBY} (Lng)",
            playerBGuess.X, playerBGuess.Y);

        match = await matchManager.GetMatchAsync(match.Id);

        if (match == null)
        {
            logger.LogWarning("Match {MatchId} not found when trying to end round", match.Id);
            return Result<SubmitGuessResponse>.Failure(
                new Error("Match.NotFound", "Match not found when ending round.", ErrorType.NotFound));
        }

        if (match.CurrentGameRound == null || match.CurrentGameRound.Id != currentRoundId)
        {
            logger.LogInformation("Round {RoundId} was already ended for match {MatchId}. Skipping duplicate end.",
                currentRoundId, match.Id);
            return Result<SubmitGuessResponse>.Success(
                new SubmitGuessResponse(RoundEnded: true, MatchEnded: false));
        }

        var gameResponse = await guessStorage.GetCorrectAnswerAsync(match.Id, currentRoundId);

        if (gameResponse == null)
        {
            logger.LogError("❌ [Handler] Resposta correta não encontrada para match {MatchId}, round {RoundId}",
                match.Id, currentRoundId);
            return Result<SubmitGuessResponse>.Failure(
                new Error("Round.AnswerNotFound", "Round data corrupted.", ErrorType.NotFound));
        }

        logger.LogInformation("🎯 [Handler] Resposta Correta: X={CorrectX} (Lat), Y={CorrectY} (Lng)",
            gameResponse.X, gameResponse.Y);

        match.EndCurrentGameRound(gameResponse, playerAGuess, playerBGuess);
        await matchManager.UpdateMatchAsync(match);

        var lastRound = match.GameRounds?.Last();
        if (lastRound != null)
        {
            logger.LogInformation("🏆 [Handler] Rodada finalizada - PlayerA: {PlayerAPoints} pts, PlayerB: {PlayerBPoints} pts",
                lastRound.PlayerAPoints, lastRound.PlayerBPoints);
        }

        await guessStorage.ClearGuessesAsync(match.Id, currentRoundId);

        if (match.GameRounds == null || !match.GameRounds.Any())
        {
            return Result<SubmitGuessResponse>.Failure(
                new Error("Match.InvalidState", "Match must have rounds after ending a round.", ErrorType.Validation));
        }

        var endedRound = match.GameRounds.Last();
        var roundResult = new RoundEndResult(
            RoundId: endedRound.Id,
            RoundNumber: match.GameRounds.Count,
            CorrectLocation: gameResponse,
            PlayerA: new PlayerGuessResult(
                PlayerId: match.PlayerAId,
                Guess: playerAGuess,
                Points: endedRound.PlayerAPoints ?? 0,
                DistanceInKm: gameResponse.CalculateDistance(playerAGuess)
            ),
            PlayerB: new PlayerGuessResult(
                PlayerId: match.PlayerBId,
                Guess: playerBGuess,
                Points: endedRound.PlayerBPoints ?? 0,
                DistanceInKm: gameResponse.CalculateDistance(playerBGuess)
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
            logger.LogError(ex, "Failed to publish RoundEnded event for match {MatchId}. Continuing anyway.", match.Id);
        }

        if (!match.CanStartNewRound())
        {
            logger.LogInformation("Match {MatchId} is complete. Ending match.", match.Id);

            match.EndGameMatch();
            await matchManager.UpdateMatchAsync(match);

            var matchResult = new MatchEndResult(
                MatchId: match.Id,
                WinnerId: match.PlayerWinnerId ?? Guid.Empty,
                PlayerAFinalPoints: match.PlayerATotalPoints ?? 0,
                PlayerBFinalPoints: match.PlayerBTotalPoints ?? 0
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
                        var success = await geoDataClient.SendMatchEndedAsync(matchEvent);
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

            return Result<SubmitGuessResponse>.Success(
                new SubmitGuessResponse(RoundEnded: true, MatchEnded: true, roundResult, matchResult));
        }

        return Result<SubmitGuessResponse>.Success(
            new SubmitGuessResponse(RoundEnded: true, MatchEnded: false, roundResult));
    }
}
