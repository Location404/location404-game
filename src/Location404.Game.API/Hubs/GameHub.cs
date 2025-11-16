namespace Location404.Game.API.Hubs;

using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Location404.Game.Application.Services;
using Location404.Game.Application.DTOs.Requests;
using Location404.Game.Application.DTOs.Responses;
using Location404.Game.Application.Events;
using Shared.Observability.Core;

[Authorize]
public class GameHub(
    IGameMatchManager matchManager,
    IMatchmakingService matchmaking,
    IGameEventPublisher eventPublisher,
    IGuessStorageManager guessStorage,
    IPlayerConnectionManager connectionManager,
    IGeoDataClient geoDataClient,
    IRoundTimerService roundTimer,
    ActivitySource activitySource,
    ObservabilityMetrics metrics,
    ILogger<GameHub> logger) : Hub
{
    private readonly IGameMatchManager _matchManager = matchManager;
    private readonly IMatchmakingService _matchmaking = matchmaking;
    private readonly IGameEventPublisher _eventPublisher = eventPublisher;
    private readonly IGuessStorageManager _guessStorage = guessStorage;
    private readonly IPlayerConnectionManager _connectionManager = connectionManager;
    private readonly IGeoDataClient _geoDataClient = geoDataClient;
    private readonly IRoundTimerService _roundTimer = roundTimer;
    private readonly ActivitySource _activitySource = activitySource;
    private readonly ObservabilityMetrics _metrics = metrics;
    private readonly ILogger<GameHub> _logger = logger;

    public async Task<string> JoinMatchmaking(JoinMatchmakingRequest request)
    {
        using var activity = _activitySource.StartActivity("JoinMatchmaking", ActivityKind.Server);
        activity?.SetTag("game.player_id", request.PlayerId.ToString());
        activity?.SetTag("signalr.method", "JoinMatchmaking");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Player {PlayerId} joining matchmaking queue", request.PlayerId);

            await _connectionManager.MapPlayerToConnectionAsync(request.PlayerId, Context.ConnectionId);
            _logger.LogInformation("Mapped Player {PlayerId} to ConnectionId {ConnectionId}", request.PlayerId, Context.ConnectionId);

            var isInMatch = await _matchManager.IsPlayerInMatchAsync(request.PlayerId);
            if (isInMatch)
            {
                _logger.LogWarning("Player {PlayerId} is already in an active match. Force cleaning up to allow new matchmaking...", request.PlayerId);

                var existingMatch = await _matchManager.GetPlayerCurrentMatchAsync(request.PlayerId);
                if (existingMatch != null)
                {
                    var isMatchEnded = existingMatch.EndTime != default(DateTime);
                    var roundCount = existingMatch.GameRounds?.Count ?? 0;

                    _logger.LogInformation("Match {MatchId} status - Ended: {Ended}, Rounds: {Rounds}/3",
                        existingMatch.Id, isMatchEnded, roundCount);

                    if (!isMatchEnded)
                    {
                        _logger.LogWarning("Match {MatchId} was not properly ended. Finalizing as interrupted...", existingMatch.Id);
                        existingMatch.EndGameMatch();
                        await _matchManager.UpdateMatchAsync(existingMatch);
                        _logger.LogInformation("Match {MatchId} finalized as interrupted", existingMatch.Id);
                    }

                    _logger.LogWarning("Removing match {MatchId} to allow player {PlayerId} to join new matchmaking", existingMatch.Id, request.PlayerId);
                    await _matchManager.RemoveMatchAsync(existingMatch.Id);
                    _logger.LogInformation("Player {PlayerId} freed from previous match. Continuing to matchmaking...", request.PlayerId);
                }
                else
                {
                    _logger.LogWarning("Player {PlayerId} in match but match not found. Clearing stale state...", request.PlayerId);
                    await _matchManager.ClearPlayerMatchStateAsync(request.PlayerId);
                }
            }

            _logger.LogInformation("Player {PlayerId} passed all checks. Calling JoinQueueAsync...", request.PlayerId);
            await _matchmaking.JoinQueueAsync(request.PlayerId);

            _logger.LogInformation("Player {PlayerId} joined queue. Calling TryFindMatchAsync...", request.PlayerId);
            var match = await _matchmaking.TryFindMatchAsync();

            if (match != null)
            {
                _logger.LogInformation("Match {MatchId} created for players {PlayerA} and {PlayerB}",
                    match.Id, match.PlayerAId, match.PlayerBId);

                var playerAConnectionId = await _connectionManager.GetConnectionIdAsync(match.PlayerAId);
                var playerBConnectionId = await _connectionManager.GetConnectionIdAsync(match.PlayerBId);

                if (playerAConnectionId != null)
                {
                    await Groups.AddToGroupAsync(playerAConnectionId, match.Id.ToString());
                    _logger.LogInformation("Added PlayerA {PlayerId} to match group {MatchId}", match.PlayerAId, match.Id);
                }
                else
                {
                    _logger.LogWarning("PlayerA {PlayerId} connection not found", match.PlayerAId);
                }

                if (playerBConnectionId != null)
                {
                    await Groups.AddToGroupAsync(playerBConnectionId, match.Id.ToString());
                    _logger.LogInformation("Added PlayerB {PlayerId} to match group {MatchId}", match.PlayerBId, match.Id);
                }
                else
                {
                    _logger.LogWarning("PlayerB {PlayerId} connection not found", match.PlayerBId);
                }

                var response = new MatchFoundResponse(
                    match.Id,
                    match.PlayerAId,
                    match.PlayerBId,
                    match.StartTime
                );

                await Clients.Group(match.Id.ToString())
                    .SendAsync("MatchFound", response);

                return "Match found!";
            }

            _logger.LogInformation("Player {PlayerId} added to matchmaking queue", request.PlayerId);

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

    public async Task StartRound(StartRoundRequest request)
    {
        try
        {
            _logger.LogInformation("Starting round for match {MatchId}", request.MatchId);

            var match = await _matchManager.GetMatchAsync(request.MatchId);

            if (match == null)
            {
                _logger.LogWarning("Match {MatchId} not found", request.MatchId);
                await Clients.Caller.SendAsync("Error", "Match not found.");
                return;
            }

            if (!match.CanStartNewRound())
            {
                _logger.LogWarning("Cannot start new round for match {MatchId}. Current round count: {RoundCount}",
                    request.MatchId, match.GameRounds?.Count ?? 0);
                await Clients.Caller.SendAsync("Error", "Cannot start new round.");
                return;
            }

            match.StartNewGameRound();
            await _matchManager.UpdateMatchAsync(match);

            var locationDto = await _geoDataClient.GetRandomLocationAsync();

            LocationData location;
            if (locationDto != null)
            {
                location = new LocationData(
                    locationDto.Coordinate.X,
                    locationDto.Coordinate.Y,
                    locationDto.Heading ?? new Random().Next(0, 360),
                    locationDto.Pitch ?? new Random().Next(-10, 10)
                );

                _logger.LogInformation("Using location from geo-data-service: {Name}", locationDto.Name);
            }
            else
            {
                _logger.LogWarning("geo-data-service unavailable, using fallback hardcoded location");
                location = GenerateRandomLocation();
            }

            await _guessStorage.StoreCorrectAnswerAsync(
                match.Id,
                match.CurrentGameRound!.Id,
                location.ToCoordinate()
            );

            var startedAt = DateTimeOffset.UtcNow;
            var durationSeconds = 90;

            var response = new RoundStartedResponse(
                match.Id,
                match.CurrentGameRound!.Id,
                match.CurrentGameRound.RoundNumber,
                DateTime.UtcNow,
                location,
                startedAt,
                durationSeconds
            );

            _logger.LogInformation("Round {RoundNumber} started for match {MatchId} at location ({X}, {Y})",
                match.CurrentGameRound.RoundNumber, request.MatchId, location.X, location.Y);

            await Clients.Group(match.Id.ToString())
                .SendAsync("RoundStarted", response);

            await _roundTimer.StartTimerAsync(match.Id, match.CurrentGameRound!.Id, TimeSpan.FromSeconds(durationSeconds));
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
            _logger.LogInformation("Player {PlayerId} submitting guess for match {MatchId}",
                request.PlayerId, request.MatchId);

            var match = await _matchManager.GetMatchAsync(request.MatchId);

            if (match == null)
            {
                _logger.LogWarning("Match {MatchId} not found", request.MatchId);
                await Clients.Caller.SendAsync("Error", "Match not found.");
                return;
            }

            if (match.CurrentGameRound == null)
            {
                _logger.LogWarning("No active round for match {MatchId}", request.MatchId);
                await Clients.Caller.SendAsync("Error", "No active round.");
                return;
            }

            // Save the roundId before any operations that might modify CurrentGameRound
            var currentRoundId = match.CurrentGameRound.Id;

            var guess = request.ToCoordinate();

            _logger.LogInformation("📥 [GameHub] Palpite recebido de {PlayerId}: X={X} (Lat), Y={Y} (Lng)",
                request.PlayerId, request.X, request.Y);

            await _guessStorage.StoreGuessAsync(
                request.MatchId,
                currentRoundId,
                request.PlayerId,
                guess
            );

            _logger.LogInformation("✅ [GameHub] Palpite armazenado para player {PlayerId} em ({X}, {Y})",
                request.PlayerId, request.X, request.Y);

            await Clients.Caller.SendAsync("GuessSubmitted", "Guess submitted successfully.");

            var (playerAGuess, playerBGuess) = await _guessStorage.GetBothGuessesAsync(
                request.MatchId,
                currentRoundId,
                match.PlayerAId,
                match.PlayerBId
            );

            var isFirstGuess = (playerAGuess != null && playerBGuess == null) || (playerAGuess == null && playerBGuess != null);

            if (isFirstGuess)
            {
                var remainingTime = await _roundTimer.GetRemainingTimeAsync(request.MatchId, currentRoundId);

                if (remainingTime.HasValue && remainingTime.Value.TotalSeconds > 15)
                {
                    _logger.LogInformation("⏱️ [GameHub] Primeiro palpite detectado. Ajustando timer de {Current}s para 15s",
                        remainingTime.Value.TotalSeconds);

                    await _roundTimer.AdjustTimerAsync(request.MatchId, currentRoundId, TimeSpan.FromSeconds(15));

                    await Clients.Group(match.Id.ToString()).SendAsync("TimerAdjusted", new
                    {
                        matchId = request.MatchId,
                        roundId = currentRoundId,
                        newDuration = 15,
                        adjustedAt = DateTimeOffset.UtcNow
                    });
                }
                else if (remainingTime.HasValue)
                {
                    _logger.LogInformation("⏱️ [GameHub] Primeiro palpite detectado, mas timer já está em {Current}s (≤15s). Mantendo tempo atual.",
                        remainingTime.Value.TotalSeconds);
                }

                var opponentId = request.PlayerId == match.PlayerAId ? match.PlayerBId : match.PlayerAId;
                await Clients.Group(match.Id.ToString()).SendAsync("OpponentSubmitted", new
                {
                    playerId = request.PlayerId,
                    opponentId = opponentId
                });
            }

            if (playerAGuess != null && playerBGuess != null)
            {
                await _roundTimer.CancelTimerAsync(request.MatchId, currentRoundId);

                _logger.LogInformation("✅ [GameHub] Ambos jogadores enviaram palpites para match {MatchId}. Finalizando rodada...",
                    request.MatchId);
                _logger.LogInformation("📍 [GameHub] PlayerA Guess: X={PlayerAX} (Lat), Y={PlayerAY} (Lng)",
                    playerAGuess.X, playerAGuess.Y);
                _logger.LogInformation("📍 [GameHub] PlayerB Guess: X={PlayerBX} (Lat), Y={PlayerBY} (Lng)",
                    playerBGuess.X, playerBGuess.Y);

                // Re-fetch match to ensure we have the latest state (prevent race condition)
                match = await _matchManager.GetMatchAsync(request.MatchId);

                if (match == null)
                {
                    _logger.LogWarning("Match {MatchId} not found when trying to end round", request.MatchId);
                    return;
                }

                // Check if round was already ended by another concurrent request
                if (match.CurrentGameRound == null || match.CurrentGameRound.Id != currentRoundId)
                {
                    _logger.LogInformation("Round {RoundId} was already ended for match {MatchId}. Skipping duplicate end.",
                        currentRoundId, request.MatchId);
                    return;
                }

                var gameResponse = await _guessStorage.GetCorrectAnswerAsync(
                    request.MatchId,
                    currentRoundId
                );

                if (gameResponse == null)
                {
                    _logger.LogError("❌ [GameHub] Resposta correta não encontrada para match {MatchId}, round {RoundId}",
                        request.MatchId, currentRoundId);
                    await Clients.Caller.SendAsync("Error", "Round data corrupted. Please restart the match.");
                    return;
                }

                _logger.LogInformation("🎯 [GameHub] Resposta Correta: X={CorrectX} (Lat), Y={CorrectY} (Lng)",
                    gameResponse.X, gameResponse.Y);

                match.EndCurrentGameRound(gameResponse, playerAGuess, playerBGuess);
                await _matchManager.UpdateMatchAsync(match);

                var lastRound = match.GameRounds?.Last();
                if (lastRound != null)
                {
                    _logger.LogInformation("🏆 [GameHub] Rodada finalizada - PlayerA: {PlayerAPoints} pts, PlayerB: {PlayerBPoints} pts",
                        lastRound.PlayerAPoints, lastRound.PlayerBPoints);
                }

                await _guessStorage.ClearGuessesAsync(request.MatchId, currentRoundId);

                if (match.GameRounds == null || !match.GameRounds.Any())
                    throw new InvalidOperationException("Match must have rounds after ending a round.");

                var roundEndedResponse = RoundEndedResponse.FromGameRound(
                    match.GameRounds.Last(),
                    match.PlayerATotalPoints,
                    match.PlayerBTotalPoints
                );

                await Clients.Group(match.Id.ToString()).SendAsync("RoundEnded", roundEndedResponse);

                _logger.LogInformation("Round {RoundNumber} ended for match {MatchId}. PlayerA: {PlayerAPoints}, PlayerB: {PlayerBPoints}",
                    match.GameRounds.Count, request.MatchId, match.PlayerATotalPoints, match.PlayerBTotalPoints);

                try
                {
                    var roundEvent = GameRoundEndedEvent.FromGameRound(match.GameRounds.Last());
                    await _eventPublisher.PublishRoundEndedAsync(roundEvent);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish RoundEnded event for match {MatchId}. Continuing anyway.", request.MatchId);
                }

                if (!match.CanStartNewRound())
                {
                    _logger.LogInformation("Match {MatchId} is complete. Ending match.", request.MatchId);

                    match.EndGameMatch();
                    await _matchManager.UpdateMatchAsync(match);

                    var matchEndedResponse = MatchEndedResponse.FromGameMatch(match);

                    await Clients.Group(match.Id.ToString())
                        .SendAsync("MatchEnded", matchEndedResponse);

                    _logger.LogInformation("Match {MatchId} ended. Winner: {WinnerId}",
                        request.MatchId, match.PlayerWinnerId);

                    var matchEvent = GameMatchEndedEvent.FromGameMatch(match);

                    try
                    {
                        await _eventPublisher.PublishMatchEndedAsync(matchEvent);
                        _logger.LogInformation("Match ended event published to RabbitMQ for match {MatchId}", request.MatchId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to publish MatchEnded event to RabbitMQ for match {MatchId}. Trying HTTP fallback...", request.MatchId);

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var success = await _geoDataClient.SendMatchEndedAsync(matchEvent);
                                if (!success)
                                {
                                    _logger.LogError("HTTP fallback also failed for match {MatchId}. Match data may not be persisted!", request.MatchId);
                                }
                            }
                            catch (Exception httpEx)
                            {
                                _logger.LogError(httpEx, "HTTP fallback threw exception for match {MatchId}", request.MatchId);
                            }
                        });
                    }

                    _logger.LogInformation("Removing match {MatchId} from cache...", match.Id);
                    await _matchManager.RemoveMatchAsync(match.Id);
                    _logger.LogInformation("Match {MatchId} removed successfully from cache", match.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting guess for player {PlayerId} in match {MatchId}",
                request.PlayerId, request.MatchId);
            await Clients.Caller.SendAsync("Error", $"Error submitting guess: {ex.Message}");
        }
    }

    public async Task EndRound(EndRoundRequest request)
    {
        try
        {
            _logger.LogInformation("Ending round for match {MatchId}", request.MatchId);

            var match = await _matchManager.GetMatchAsync(request.MatchId);

            if (match == null)
            {
                _logger.LogWarning("Match {MatchId} not found when ending round", request.MatchId);
                await Clients.Caller.SendAsync("Error", "Match not found.");
                return;
            }

            if (match.CurrentGameRound == null)
            {
                _logger.LogWarning("No active round for match {MatchId}", request.MatchId);
                await Clients.Caller.SendAsync("Error", "No active round to end.");
                return;
            }

            var gameResponse = request.GetGameResponse();
            var playerAGuess = request.GetPlayerAGuess();
            var playerBGuess = request.GetPlayerBGuess();

            match.EndCurrentGameRound(gameResponse, playerAGuess, playerBGuess);
            await _matchManager.UpdateMatchAsync(match);

            if (match.GameRounds == null || !match.GameRounds.Any())
                throw new InvalidOperationException("Match must have rounds after ending a round.");

            var roundEndedResponse = RoundEndedResponse.FromGameRound(
                match.GameRounds.Last(),
                match.PlayerATotalPoints,
                match.PlayerBTotalPoints
            );

            await Clients.Group(match.Id.ToString()).SendAsync("RoundEnded", roundEndedResponse);

            _logger.LogInformation("Round {RoundNumber} ended for match {MatchId}. PlayerA: {PlayerAPoints}, PlayerB: {PlayerBPoints}",
                match.GameRounds.Count, request.MatchId, match.PlayerATotalPoints, match.PlayerBTotalPoints);

            try
            {
                var roundEvent = GameRoundEndedEvent.FromGameRound(match.GameRounds.Last());
                await _eventPublisher.PublishRoundEndedAsync(roundEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish RoundEnded event for match {MatchId}. Continuing anyway.", request.MatchId);
            }

            if (!match.CanStartNewRound())
            {
                _logger.LogInformation("Match {MatchId} is complete. Ending match.", request.MatchId);

                match.EndGameMatch();
                await _matchManager.UpdateMatchAsync(match);

                var matchEndedResponse = MatchEndedResponse.FromGameMatch(match);

                await Clients.Group(match.Id.ToString())
                    .SendAsync("MatchEnded", matchEndedResponse);

                _logger.LogInformation("Match {MatchId} ended. Winner: {WinnerId}",
                    request.MatchId, match.PlayerWinnerId);

                try
                {
                    var matchEvent = GameMatchEndedEvent.FromGameMatch(match);
                    await _eventPublisher.PublishMatchEndedAsync(matchEvent);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish MatchEnded event for match {MatchId}. Match data may not be persisted.", request.MatchId);
                }

                await _matchManager.RemoveMatchAsync(match.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending round for match {MatchId}", request.MatchId);
            await Clients.Caller.SendAsync("Error", $"Error ending round: {ex.Message}");
        }
    }

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

    private Guid GetPlayerIdFromContext()
    {
        var playerIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value
            ?? Context.User?.FindFirst("PlayerId")?.Value;

        if (string.IsNullOrEmpty(playerIdClaim))
            return Guid.Empty;

        return Guid.TryParse(playerIdClaim, out var playerId) ? playerId : Guid.Empty;
    }

    // Static Random instance to avoid seed collision bug
    private static readonly Random _random = new Random();

    private LocationData GenerateRandomLocation()
    {
        var locations = new[]
        {
            // South America - Brazil
            new { X = -23.5505, Y = -46.6333, Name = "São Paulo, Brazil" },
            new { X = -22.9068, Y = -43.1729, Name = "Rio de Janeiro, Brazil" },
            new { X = -15.7942, Y = -47.8822, Name = "Brasília, Brazil" },
            new { X = -12.9714, Y = -38.5014, Name = "Salvador, Brazil" },
            new { X = -3.1190, Y = -60.0217, Name = "Manaus, Brazil" },
            new { X = -25.4284, Y = -49.2733, Name = "Curitiba, Brazil" },

            // South America - Other
            new { X = -34.6037, Y = -58.3816, Name = "Buenos Aires, Argentina" },
            new { X = -33.4489, Y = -70.6693, Name = "Santiago, Chile" },
            new { X = -12.0464, Y = -77.0428, Name = "Lima, Peru" },
            new { X = 4.7110, Y = -74.0721, Name = "Bogotá, Colombia" },

            // North America - USA
            new { X = 40.7580, Y = -73.9855, Name = "New York, USA" },
            new { X = 37.7749, Y = -122.4194, Name = "San Francisco, USA" },
            new { X = 34.0522, Y = -118.2437, Name = "Los Angeles, USA" },
            new { X = 41.8781, Y = -87.6298, Name = "Chicago, USA" },
            new { X = 25.7617, Y = -80.1918, Name = "Miami, USA" },
            new { X = 47.6062, Y = -122.3321, Name = "Seattle, USA" },

            // North America - Other
            new { X = 19.4326, Y = -99.1332, Name = "Mexico City, Mexico" },
            new { X = 43.6532, Y = -79.3832, Name = "Toronto, Canada" },
            new { X = 49.2827, Y = -123.1207, Name = "Vancouver, Canada" },

            // Europe - Western
            new { X = 48.8566, Y = 2.3522, Name = "Paris, France" },
            new { X = 51.5074, Y = -0.1278, Name = "London, UK" },
            new { X = 52.3676, Y = 4.9041, Name = "Amsterdam, Netherlands" },
            new { X = 50.8503, Y = 4.3517, Name = "Brussels, Belgium" },
            new { X = 41.3851, Y = 2.1734, Name = "Barcelona, Spain" },
            new { X = 40.4168, Y = -3.7038, Name = "Madrid, Spain" },
            new { X = 38.7223, Y = -9.1393, Name = "Lisbon, Portugal" },

            // Europe - Central & Eastern
            new { X = 41.9028, Y = 12.4964, Name = "Rome, Italy" },
            new { X = 52.5200, Y = 13.4050, Name = "Berlin, Germany" },
            new { X = 48.2082, Y = 16.3738, Name = "Vienna, Austria" },
            new { X = 50.0755, Y = 14.4378, Name = "Prague, Czech Republic" },
            new { X = 59.3293, Y = 18.0686, Name = "Stockholm, Sweden" },
            new { X = 55.6761, Y = 12.5683, Name = "Copenhagen, Denmark" },
            new { X = 52.2297, Y = 21.0122, Name = "Warsaw, Poland" },

            // Europe - Southern & Eastern
            new { X = 37.9838, Y = 23.7275, Name = "Athens, Greece" },
            new { X = 41.0082, Y = 28.9784, Name = "Istanbul, Turkey" },
            new { X = 55.7558, Y = 37.6173, Name = "Moscow, Russia" },

            // Asia - East
            new { X = 35.6762, Y = 139.6503, Name = "Tokyo, Japan" },
            new { X = 37.5665, Y = 126.9780, Name = "Seoul, South Korea" },
            new { X = 39.9042, Y = 116.4074, Name = "Beijing, China" },
            new { X = 31.2304, Y = 121.4737, Name = "Shanghai, China" },
            new { X = 22.3193, Y = 114.1694, Name = "Hong Kong" },
            new { X = 25.0330, Y = 121.5654, Name = "Taipei, Taiwan" },

            // Asia - Southeast
            new { X = 1.3521, Y = 103.8198, Name = "Singapore" },
            new { X = 13.7563, Y = 100.5018, Name = "Bangkok, Thailand" },
            new { X = -6.2088, Y = 106.8456, Name = "Jakarta, Indonesia" },
            new { X = 14.5995, Y = 120.9842, Name = "Manila, Philippines" },
            new { X = 21.0285, Y = 105.8542, Name = "Hanoi, Vietnam" },

            // Asia - South & Middle East
            new { X = 28.6139, Y = 77.2090, Name = "New Delhi, India" },
            new { X = 19.0760, Y = 72.8777, Name = "Mumbai, India" },
            new { X = 25.2048, Y = 55.2708, Name = "Dubai, UAE" },
            new { X = 33.8938, Y = 35.5018, Name = "Beirut, Lebanon" },

            // Africa
            new { X = 30.0444, Y = 31.2357, Name = "Cairo, Egypt" },
            new { X = -26.2041, Y = 28.0473, Name = "Johannesburg, South Africa" },
            new { X = -33.9249, Y = 18.4241, Name = "Cape Town, South Africa" },
            new { X = -1.2921, Y = 36.8219, Name = "Nairobi, Kenya" },

            // Oceania
            new { X = -33.8688, Y = 151.2093, Name = "Sydney, Australia" },
            new { X = -37.8136, Y = 144.9631, Name = "Melbourne, Australia" },
            new { X = -41.2865, Y = 174.7762, Name = "Wellington, New Zealand" },
        };

        var location = locations[_random.Next(locations.Length)];
        var heading = _random.Next(0, 360);
        var pitch = _random.Next(-10, 10);

        _logger.LogInformation("Generated location: {Name} (X:{X}, Y:{Y}, Heading:{Heading}°)",
            location.Name, location.X, location.Y, heading);

        return new LocationData(location.X, location.Y, heading, pitch);
    }
}