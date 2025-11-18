namespace Location404.Game.API.BackgroundServices;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Microsoft.AspNetCore.SignalR;
using Location404.Game.API.Hubs;
using Location404.Game.Application.Services;
using Location404.Game.Application.DTOs.Responses;
using Location404.Game.Application.Features.GameRounds.Commands;
using Location404.Game.Application.Common.Result;
using LiteBus.Commands.Abstractions;

public class RoundTimerExpirationListener : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RoundTimerExpirationListener> _logger;
    private ISubscriber? _subscriber;

    public RoundTimerExpirationListener(
        IConnectionMultiplexer redis,
        IServiceProvider serviceProvider,
        ILogger<RoundTimerExpirationListener> logger)
    {
        _redis = redis;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _subscriber = _redis.GetSubscriber();

            var channel = new RedisChannel("__keyevent@0__:expired", RedisChannel.PatternMode.Literal);

            await _subscriber.SubscribeAsync(channel, async (ch, expiredKey) =>
            {
                try
                {
                    var key = expiredKey.ToString();

                    if (!key.StartsWith("round:timer:"))
                        return;

                    var parts = key.Split(':');
                    if (parts.Length != 4)
                    {
                        _logger.LogWarning("⏱️ [TimerExpiration] Invalid key format: {Key}", key);
                        return;
                    }

                    if (!Guid.TryParse(parts[2], out var matchId) || !Guid.TryParse(parts[3], out var roundId))
                    {
                        _logger.LogWarning("⏱️ [TimerExpiration] Failed to parse GUIDs from key: {Key}", key);
                        return;
                    }

                    _logger.LogWarning("⏱️ [TimerExpiration] Timer expired for match {MatchId}, round {RoundId}. Processing auto-submit...",
                        matchId, roundId);

                    await ProcessTimerExpiration(matchId, roundId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "⏱️ [TimerExpiration] Error processing expired key: {Key}", expiredKey);
                }
            });

            _logger.LogInformation("⏱️ [TimerExpiration] Listening for Redis keyspace notifications on __keyevent@0__:expired");

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("⏱️ [TimerExpiration] Listener stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "⏱️ [TimerExpiration] Fatal error in listener");
        }
    }

    private async Task ProcessTimerExpiration(Guid matchId, Guid roundId)
    {
        using var scope = _serviceProvider.CreateScope();

        var matchManager = scope.ServiceProvider.GetRequiredService<IGameMatchManager>();
        var guessStorage = scope.ServiceProvider.GetRequiredService<IGuessStorageManager>();
        var endRoundHandler = scope.ServiceProvider.GetRequiredService<ICommandHandler<EndRoundCommand, Result<EndRoundResponse>>>();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<GameHub>>();

        try
        {
            var match = await matchManager.GetMatchAsync(matchId);

            if (match == null)
            {
                _logger.LogWarning("⏱️ [ForceRoundEnd] Match {MatchId} not found", matchId);
                return;
            }

            if (match.CurrentGameRound == null || match.CurrentGameRound.Id != roundId)
            {
                _logger.LogInformation("⏱️ [ForceRoundEnd] Round {RoundId} already ended for match {MatchId}", roundId, matchId);
                return;
            }

            var (playerAGuess, playerBGuess) = await guessStorage.GetBothGuessesAsync(
                matchId,
                roundId,
                match.PlayerAId,
                match.PlayerBId
            );

            if (playerAGuess != null && playerBGuess != null)
            {
                _logger.LogInformation("⏱️ [ForceRoundEnd] Both players submitted for match {MatchId}, round {RoundId}. Skipping force end.", matchId, roundId);
                return;
            }

            var gameResponse = await guessStorage.GetCorrectAnswerAsync(matchId, roundId);

            if (gameResponse == null)
            {
                _logger.LogError("⏱️ [ForceRoundEnd] Correct answer not found for match {MatchId}, round {RoundId}", matchId, roundId);
                return;
            }

            _logger.LogInformation("⏱️ [ForceRoundEnd] Ending round with null guesses - PlayerA: {PlayerANull}, PlayerB: {PlayerBNull}",
                playerAGuess == null ? "NULL" : "SUBMITTED",
                playerBGuess == null ? "NULL" : "SUBMITTED");

            var endRoundCommand = new EndRoundCommand(
                MatchId: matchId,
                RoundId: roundId,
                PlayerAGuess: playerAGuess ?? gameResponse,
                PlayerBGuess: playerBGuess ?? gameResponse
            );

            var result = await endRoundHandler.HandleAsync(endRoundCommand);

            if (result.IsFailure)
            {
                _logger.LogError("⏱️ [ForceRoundEnd] Failed to end round: {Error}", result.Error.Message);
                return;
            }

            if (result.Value.RoundEnded && result.Value.RoundResult != null)
            {
                var roundEndedResponse = RoundEndedResponse.FromRoundEndResult(result.Value.RoundResult);
                await hubContext.Clients.Group(matchId.ToString()).SendAsync("RoundEnded", roundEndedResponse);

                _logger.LogInformation("⏱️ [ForceRoundEnd] Round {RoundNumber} ended for match {MatchId}. PlayerA: {PlayerAPoints}, PlayerB: {PlayerBPoints}",
                    result.Value.RoundResult.RoundNumber, matchId,
                    result.Value.RoundResult.PlayerATotalPoints,
                    result.Value.RoundResult.PlayerBTotalPoints);
            }

            if (result.Value.MatchEnded && result.Value.MatchResult != null)
            {
                var matchEndedResponse = MatchEndedResponse.FromMatchEndResult(result.Value.MatchResult);
                await hubContext.Clients.Group(matchId.ToString()).SendAsync("MatchEnded", matchEndedResponse);

                _logger.LogInformation("⏱️ [ForceRoundEnd] Match {MatchId} ended. Winner: {WinnerId}",
                    matchId, result.Value.MatchResult.WinnerId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "⏱️ [ForceRoundEnd] Error force-ending round for match {MatchId}, round {RoundId}", matchId, roundId);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_subscriber != null)
        {
            await _subscriber.UnsubscribeAllAsync();
            _logger.LogInformation("⏱️ [TimerExpiration] Unsubscribed from Redis keyspace notifications");
        }

        await base.StopAsync(cancellationToken);
    }
}
