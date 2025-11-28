using LiteBus.Commands.Abstractions;
using Location404.Game.Application.Common.Result;
using Location404.Game.Application.Features.GameRounds.Commands.EndRoundCommand;
using Location404.Game.Application.Features.GameRounds.Interfaces;
using Location404.Game.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Location404.Game.Application.Features.GameRounds.Commands.SubmitGuessCommand;

public class SubmitGuessCommandHandler(
    IGameMatchManager matchManager,
    IGuessStorageManager guessStorage,
    IRoundTimerService roundTimer,
    ICommandHandler<EndRoundCommand.EndRoundCommand, Result<EndRoundCommandResponse>> endRoundHandler,
    ILogger<SubmitGuessCommandHandler> logger
) : ICommandHandler<SubmitGuessCommand, Result<SubmitGuessCommandResponse>>
{
    public async Task<Result<SubmitGuessCommandResponse>> HandleAsync(
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
                return Result<SubmitGuessCommandResponse>.Failure(
                    new Error("Match.NotFound", "Match not found.", ErrorType.NotFound));
            }

            if (match.CurrentGameRound == null)
            {
                logger.LogWarning("No active round for match {MatchId}", command.MatchId);
                return Result<SubmitGuessCommandResponse>.Failure(
                    new Error("Round.NotActive", "No active round.", ErrorType.Validation));
            }

            var currentRoundId = match.CurrentGameRound.Id;

            logger.LogInformation("[Handler] Palpite recebido de {PlayerId}: X={X} (Lat), Y={Y} (Lng)",
                command.PlayerId, command.Guess.X, command.Guess.Y);

            await guessStorage.StoreGuessAsync(
                command.MatchId,
                currentRoundId,
                command.PlayerId,
                command.Guess
            );

            logger.LogInformation("[Handler] Palpite armazenado para player {PlayerId}",
                command.PlayerId);

            var (playerAGuess, playerBGuess) = await guessStorage.GetBothGuessesAsync(
                command.MatchId,
                currentRoundId,
                match.PlayerAId,
                match.PlayerBId
            );

            var isFirstGuess = (playerAGuess != null && playerBGuess == null) ||
                              (playerAGuess == null && playerBGuess != null);

            bool timerAdjusted = false;
            int? newTimerDuration = null;

            if (isFirstGuess)
            {
                var adjusted = await HandleFirstGuessSubmittedAsync(command.MatchId, currentRoundId);
                timerAdjusted = adjusted.HasValue;
                newTimerDuration = adjusted.HasValue ? (int)adjusted.Value.TotalSeconds : null;
            }

            if (playerAGuess != null && playerBGuess != null)
            {
                logger.LogInformation("[Handler] Ambos jogadores enviaram palpites para match {MatchId}. Delegando para EndRoundCommand...",
                    command.MatchId);

                var endRoundCommand = new EndRoundCommand.EndRoundCommand(
                    MatchId: command.MatchId,
                    RoundId: currentRoundId,
                    PlayerAGuess: playerAGuess,
                    PlayerBGuess: playerBGuess
                );

                var endRoundResult = await endRoundHandler.HandleAsync(endRoundCommand, cancellationToken);

                if (endRoundResult.IsFailure)
                {
                    return Result<SubmitGuessCommandResponse>.Failure(endRoundResult.Error);
                }

                return Result<SubmitGuessCommandResponse>.Success(
                    new SubmitGuessCommandResponse(
                        RoundEnded: endRoundResult.Value.RoundEnded,
                        MatchEnded: endRoundResult.Value.MatchEnded,
                        PlayerId: command.PlayerId,
                        RoundResult: endRoundResult.Value.RoundResult,
                        MatchResult: endRoundResult.Value.MatchResult,
                        TimerAdjusted: timerAdjusted,
                        NewTimerDuration: newTimerDuration,
                        RoundId: currentRoundId
                    ));
            }

            return Result<SubmitGuessCommandResponse>.Success(
                new SubmitGuessCommandResponse(
                    RoundEnded: false,
                    MatchEnded: false,
                    PlayerId: command.PlayerId,
                    TimerAdjusted: timerAdjusted,
                    NewTimerDuration: newTimerDuration,
                    RoundId: currentRoundId));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error submitting guess for player {PlayerId} in match {MatchId}",
                command.PlayerId, command.MatchId);
            return Result<SubmitGuessCommandResponse>.Failure(
                new Error("SubmitGuess.Failed", $"Error submitting guess: {ex.Message}", ErrorType.Failure));
        }
    }

    private async Task<TimeSpan?> HandleFirstGuessSubmittedAsync(Guid matchId, Guid roundId)
    {
        var remainingTime = await roundTimer.GetRemainingTimeAsync(matchId, roundId);

        if (remainingTime.HasValue && remainingTime.Value.TotalSeconds > 15)
        {
            logger.LogInformation("[Handler] Primeiro palpite detectado. Ajustando timer de {Current}s para 15s",
                remainingTime.Value.TotalSeconds);

            var newDuration = TimeSpan.FromSeconds(15);
            await roundTimer.AdjustTimerAsync(matchId, roundId, newDuration);
            return newDuration;
        }
        else if (remainingTime.HasValue)
        {
            logger.LogInformation("[Handler] Primeiro palpite detectado, mas timer já está em {Current}s (≤15s)",
                remainingTime.Value.TotalSeconds);
        }

        return null;
    }
}
