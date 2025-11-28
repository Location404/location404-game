namespace Location404.Game.API.Hubs;

using System.Diagnostics;
using System.Security.Claims;
using LiteBus.Commands.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Location404.Game.Application.Common.Result;
using Location404.Game.Application.Common.Interfaces;
using Location404.Game.Application.Features.GameRounds.Interfaces;
using Location404.Game.Application.Features.Matchmaking.Interfaces;

using Shared.Observability.Core;
using JoinMatchmakingCommand = Application.Features.Matchmaking.Commands.JoinMatchmakingCommand.JoinMatchmakingCommand;
using JoinMatchmakingCommandResponse = Application.Features.Matchmaking.Commands.JoinMatchmakingCommand.JoinMatchmakingCommandResponse;
using JoinMatchmakingRequest = Application.Features.Matchmaking.Commands.JoinMatchmakingCommand.JoinMatchmakingRequest;
using MatchFoundResponse = Application.Features.Matchmaking.Commands.JoinMatchmakingCommand.MatchFoundResponse;
using StartRoundCommand = Application.Features.GameRounds.Commands.StartRoundCommand.StartRoundCommand;
using StartRoundCommandResponse = Application.Features.GameRounds.Commands.StartRoundCommand.StartRoundCommandResponse;
using StartRoundRequest = Application.Features.GameRounds.Commands.StartRoundCommand.StartRoundRequest;
using RoundStartedResponse = Application.Features.GameRounds.Commands.StartRoundCommand.RoundStartedResponse;
using LocationData = Application.Features.GameRounds.Commands.StartRoundCommand.LocationData;
using SubmitGuessCommand = Application.Features.GameRounds.Commands.SubmitGuessCommand.SubmitGuessCommand;
using SubmitGuessCommandResponse = Application.Features.GameRounds.Commands.SubmitGuessCommand.SubmitGuessCommandResponse;
using SubmitGuessRequest = Application.Features.GameRounds.Commands.SubmitGuessCommand.SubmitGuessRequest;
using RoundEndedResponse = Application.Features.GameRounds.RoundEndedResponse;
using MatchEndedResponse = Application.Features.GameRounds.MatchEndedResponse;

[Authorize]
public class GameHub(
    ICommandHandler<JoinMatchmakingCommand, Result<JoinMatchmakingCommandResponse>> joinMatchmakingHandler,
    ICommandHandler<StartRoundCommand, Result<StartRoundCommandResponse>> startRoundHandler,
    ICommandHandler<SubmitGuessCommand, Result<SubmitGuessCommandResponse>> submitGuessHandler,
    IGameMatchManager matchManager,
    IMatchmakingService matchmaking,
    IPlayerConnectionManager connectionManager,
    ActivitySource activitySource,
    ObservabilityMetrics metrics,
    ILogger<GameHub> logger) : Hub
{
    #region Dependencies

    private readonly ICommandHandler<JoinMatchmakingCommand, Result<JoinMatchmakingCommandResponse>> _joinMatchmakingHandler = joinMatchmakingHandler;
    private readonly ICommandHandler<StartRoundCommand, Result<StartRoundCommandResponse>> _startRoundHandler = startRoundHandler;
    private readonly ICommandHandler<SubmitGuessCommand, Result<SubmitGuessCommandResponse>> _submitGuessHandler = submitGuessHandler;
    private readonly IGameMatchManager _matchManager = matchManager;
    private readonly IMatchmakingService _matchmaking = matchmaking;
    private readonly IPlayerConnectionManager _connectionManager = connectionManager;
    private readonly ActivitySource _activitySource = activitySource;
    private readonly ObservabilityMetrics _metrics = metrics;
    private readonly ILogger<GameHub> _logger = logger;

    #endregion

    #region Matchmaking (Refactored to use Command Handlers)

    public async Task<string> JoinMatchmaking(JoinMatchmakingRequest request)
    {
        using var activity = _activitySource.StartActivity("JoinMatchmaking", ActivityKind.Server);
        activity?.SetTag("game.player_id", request.PlayerId.ToString());
        activity?.SetTag("signalr.method", "JoinMatchmaking");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _connectionManager.MapPlayerToConnectionAsync(request.PlayerId, Context.ConnectionId);

            var command = new JoinMatchmakingCommand(request.PlayerId);
            var result = await _joinMatchmakingHandler.HandleAsync(command);

            if (result.IsFailure)
            {
                _logger.LogError("Failed to join matchmaking: {Error}", result.Error.Message);
                return $"Error: {result.Error.Message}";
            }

            if (result.Value.MatchFound && result.Value.Match != null)
            {
                var match = result.Value.Match;

                var playerAConnectionId = await _connectionManager.GetConnectionIdAsync(match.PlayerAId);
                var playerBConnectionId = await _connectionManager.GetConnectionIdAsync(match.PlayerBId);

                if (playerAConnectionId != null)
                    await Groups.AddToGroupAsync(playerAConnectionId, match.Id.ToString());

                if (playerBConnectionId != null)
                    await Groups.AddToGroupAsync(playerBConnectionId, match.Id.ToString());

                var response = new MatchFoundResponse(match.Id, match.PlayerAId, match.PlayerBId, match.StartTime);
                await Clients.Group(match.Id.ToString()).SendAsync("MatchFound", response);

                stopwatch.Stop();
                _metrics.RecordRequestDuration(stopwatch.Elapsed.TotalSeconds, "SignalR", "JoinMatchmaking");
                _metrics.IncrementRequests("SignalR", "JoinMatchmaking", 200);
                activity?.SetStatus(ActivityStatusCode.Ok);

                return "Match found!";
            }

            stopwatch.Stop();
            _metrics.RecordRequestDuration(stopwatch.Elapsed.TotalSeconds, "SignalR", "JoinMatchmaking");
            _metrics.IncrementRequests("SignalR", "JoinMatchmaking", 200);
            activity?.SetStatus(ActivityStatusCode.Ok);

            return "Added to queue. Waiting for opponent...";
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("exception.type", ex.GetType().FullName);
            activity?.SetTag("exception.message", ex.Message);
            _metrics.IncrementErrors(ex.GetType().Name, "JoinMatchmaking");
            _logger.LogError(ex, "Error in JoinMatchmaking for player {PlayerId}", request.PlayerId);
            return $"Error: {ex.Message}";
        }
    }

    public async Task LeaveMatchmaking(Guid playerId)
    {
        await _matchmaking.LeaveQueueAsync(playerId);
        await _connectionManager.RemoveMappingAsync(playerId);
        _logger.LogInformation("Player {PlayerId} left matchmaking and removed from connection mapping", playerId);
        await Clients.Caller.SendAsync("LeftQueue", "You have left the matchmaking queue.");
    }

    #endregion

    #region Game Rounds (Refactored to use Command Handlers)

    public async Task StartRound(StartRoundRequest request)
    {
        try
        {
            var command = new StartRoundCommand(request.MatchId);
            var result = await _startRoundHandler.HandleAsync(command);

            if (result.IsFailure)
            {
                _logger.LogError("Failed to start round: {Error}", result.Error.Message);
                await Clients.Caller.SendAsync("Error", result.Error.Message);
                return;
            }

            var startRoundData = result.Value;
            var location = new LocationData(
                startRoundData.Location.X,
                startRoundData.Location.Y,
                startRoundData.Heading ?? 0,
                startRoundData.Pitch ?? 0
            );

            var response = new RoundStartedResponse(
                startRoundData.MatchId,
                startRoundData.RoundId,
                startRoundData.RoundNumber,
                DateTime.UtcNow,
                location,
                startRoundData.StartedAt,
                startRoundData.DurationSeconds
            );

            await Clients.Group(request.MatchId.ToString()).SendAsync("RoundStarted", response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting round for match {MatchId}", request.MatchId);
            await Clients.Caller.SendAsync("Error", $"Error starting round: {ex.Message}");
        }
    }

    public async Task SubmitGuess(SubmitGuessRequest request)
    {
        try
        {
            var command = new SubmitGuessCommand(
                MatchId: request.MatchId,
                PlayerId: request.PlayerId,
                Guess: request.ToCoordinate()
            );

            var result = await _submitGuessHandler.HandleAsync(command);

            if (result.IsFailure)
            {
                _logger.LogError("Failed to submit guess: {Error}", result.Error.Message);
                await Clients.Caller.SendAsync("Error", result.Error.Message);
                return;
            }

            await Clients.Caller.SendAsync("GuessSubmitted", "Guess submitted successfully.");

            await Clients.OthersInGroup(request.MatchId.ToString()).SendAsync("OpponentSubmitted", new
            {
                playerId = result.Value.PlayerId,
                matchId = request.MatchId
            });

            if (result.Value.TimerAdjusted && result.Value.NewTimerDuration.HasValue && result.Value.RoundId.HasValue)
            {
                await Clients.Group(request.MatchId.ToString()).SendAsync("TimerAdjusted", new
                {
                    matchId = request.MatchId,
                    roundId = result.Value.RoundId.Value,
                    newDuration = result.Value.NewTimerDuration.Value,
                    adjustedAt = DateTime.UtcNow.ToString("O")
                });
            }

            if (result.Value.RoundEnded && result.Value.RoundResult != null)
            {
                var roundEndedResponse = RoundEndedResponse.FromRoundEndResult(result.Value.RoundResult);
                await Clients.Group(request.MatchId.ToString()).SendAsync("RoundEnded", roundEndedResponse);
            }

            if (result.Value.MatchEnded && result.Value.MatchResult != null)
            {
                var matchEndedResponse = MatchEndedResponse.FromMatchEndResult(result.Value.MatchResult);
                await Clients.Group(request.MatchId.ToString()).SendAsync("MatchEnded", matchEndedResponse);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting guess for player {PlayerId} in match {MatchId}",
                request.PlayerId, request.MatchId);
            await Clients.Caller.SendAsync("Error", $"Error submitting guess: {ex.Message}");
        }
    }

    #endregion

    #region Utility Methods

    public async Task GetMatchStatus(Guid matchId)
    {
        var match = await _matchManager.GetMatchAsync(matchId);

        if (match == null)
        {
            await Clients.Caller.SendAsync("Error", "Match not found.");
            return;
        }

        await Clients.Caller.SendAsync("MatchStatus", match);
    }

    #endregion

    #region SignalR Lifecycle Hooks

    public override async Task OnConnectedAsync()
    {
        var playerId = GetPlayerIdFromContext();

        if (playerId != Guid.Empty)
        {
            await _connectionManager.MapPlayerToConnectionAsync(playerId, Context.ConnectionId);
            _logger.LogInformation("Player {PlayerId} connected with ConnectionId {ConnectionId}", playerId, Context.ConnectionId);

            var match = await _matchManager.GetPlayerCurrentMatchAsync(playerId);

            if (match != null)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, match.Id.ToString());
                _logger.LogInformation("Player {PlayerId} rejoined match group {MatchId}", playerId, match.Id);
            }
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var playerId = GetPlayerIdFromContext();

        if (playerId != Guid.Empty)
        {
            await _matchmaking.LeaveQueueAsync(playerId);
            await _connectionManager.RemoveMappingAsync(playerId);
            _logger.LogInformation("Player {PlayerId} disconnected and removed from connection mapping", playerId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    #endregion

    #region Helper Methods

    private Guid GetPlayerIdFromContext()
    {
        var playerIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value
            ?? Context.User?.FindFirst("PlayerId")?.Value;

        if (string.IsNullOrEmpty(playerIdClaim))
            return Guid.Empty;

        return Guid.TryParse(playerIdClaim, out var playerId) ? playerId : Guid.Empty;
    }

    #endregion
}
