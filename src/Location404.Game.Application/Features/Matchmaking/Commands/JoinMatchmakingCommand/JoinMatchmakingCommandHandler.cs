using Location404.Game.Application.Features.Matchmaking.Interfaces;
using Location404.Game.Application.Features.GameRounds.Interfaces;
using LiteBus.Commands.Abstractions;
using Location404.Game.Application.Common.Result;
using Microsoft.Extensions.Logging;

namespace Location404.Game.Application.Features.Matchmaking.Commands.JoinMatchmakingCommand;

public class JoinMatchmakingCommandHandler(
    IMatchmakingService matchmaking,
    IGameMatchManager matchManager,
    ILogger<JoinMatchmakingCommandHandler> logger
) : ICommandHandler<JoinMatchmakingCommand, Result<JoinMatchmakingCommandResponse>>
{
    public async Task<Result<JoinMatchmakingCommandResponse>> HandleAsync(
        JoinMatchmakingCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Player {PlayerId} joining matchmaking queue", command.PlayerId);

            var isInMatch = await matchManager.IsPlayerInMatchAsync(command.PlayerId);
            if (isInMatch)
            {
                logger.LogWarning("Player {PlayerId} is already in an active match. Force cleaning up...", command.PlayerId);

                var existingMatch = await matchManager.GetPlayerCurrentMatchAsync(command.PlayerId);
                if (existingMatch != null)
                {
                    var isMatchEnded = existingMatch.EndTime != default(DateTime);
                    var roundCount = existingMatch.GameRounds?.Count ?? 0;

                    logger.LogInformation("Match {MatchId} status - Ended: {Ended}, Rounds: {Rounds}/3",
                        existingMatch.Id, isMatchEnded, roundCount);

                    if (!isMatchEnded)
                    {
                        logger.LogWarning("Match {MatchId} was not properly ended. Finalizing as interrupted...", existingMatch.Id);
                        existingMatch.EndGameMatch();
                        await matchManager.UpdateMatchAsync(existingMatch);
                        logger.LogInformation("Match {MatchId} finalized as interrupted", existingMatch.Id);
                    }

                    logger.LogWarning("Removing match {MatchId} to allow player {PlayerId} to join new matchmaking",
                        existingMatch.Id, command.PlayerId);
                    await matchManager.RemoveMatchAsync(existingMatch.Id);
                    logger.LogInformation("Player {PlayerId} freed from previous match", command.PlayerId);
                }
                else
                {
                    logger.LogWarning("Player {PlayerId} in match but match not found. Clearing stale state...", command.PlayerId);
                    await matchManager.ClearPlayerMatchStateAsync(command.PlayerId);
                }
            }

            logger.LogInformation("Player {PlayerId} passed all checks. Calling JoinQueueAsync...", command.PlayerId);
            await matchmaking.JoinQueueAsync(command.PlayerId);

            logger.LogInformation("Player {PlayerId} joined queue. Calling TryFindMatchAsync...", command.PlayerId);
            var match = await matchmaking.TryFindMatchAsync();

            if (match != null)
            {
                logger.LogInformation("Match {MatchId} created for players {PlayerA} and {PlayerB}",
                    match.Id, match.PlayerAId, match.PlayerBId);

                return Result<JoinMatchmakingCommandResponse>.Success(
                    new JoinMatchmakingCommandResponse(MatchFound: true, Match: match));
            }

            logger.LogInformation("Player {PlayerId} added to matchmaking queue", command.PlayerId);

            return Result<JoinMatchmakingCommandResponse>.Success(
                new JoinMatchmakingCommandResponse(MatchFound: false));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in JoinMatchmaking for player {PlayerId}", command.PlayerId);
            return Result<JoinMatchmakingCommandResponse>.Failure(
                new Error("Matchmaking.Failed", $"Error joining matchmaking: {ex.Message}", ErrorType.Failure));
        }
    }
}
