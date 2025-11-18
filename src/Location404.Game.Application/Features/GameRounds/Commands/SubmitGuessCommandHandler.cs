using LiteBus.Commands.Abstractions;

using Location404.Game.Application.Common.Interfaces;
using Location404.Game.Application.Common.Result;
using Location404.Game.Application.Services;
using Location404.Game.Domain.Entities;

using Microsoft.Extensions.Logging;

namespace Location404.Game.Application.Features.GameRounds.Commands;

public class SubmitGuessCommandHandler(
    IGameMatchManager matchManager,
    IGuessStorageManager guessStorage,
    IRoundTimerService roundTimer,
    ICommandHandler<EndRoundCommand, Result<EndRoundResponse>> endRoundHandler,
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
                logger.LogInformation("✅ [Handler] Ambos jogadores enviaram palpites para match {MatchId}. Delegando para EndRoundCommand...",
                    command.MatchId);

                var endRoundCommand = new EndRoundCommand(
                    MatchId: command.MatchId,
                    RoundId: currentRoundId,
                    PlayerAGuess: playerAGuess,
                    PlayerBGuess: playerBGuess
                );

                var endRoundResult = await endRoundHandler.HandleAsync(endRoundCommand, cancellationToken);

                if (endRoundResult.IsFailure)
                {
                    return Result<SubmitGuessResponse>.Failure(endRoundResult.Error);
                }

                return Result<SubmitGuessResponse>.Success(
                    new SubmitGuessResponse(
                        RoundEnded: endRoundResult.Value.RoundEnded,
                        MatchEnded: endRoundResult.Value.MatchEnded,
                        RoundResult: endRoundResult.Value.RoundResult,
                        MatchResult: endRoundResult.Value.MatchResult
                    ));
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
}
