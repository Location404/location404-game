using FluentAssertions;
using Location404.Game.Application.Common.Interfaces;
using Location404.Game.Application.Events;
using Location404.Game.Application.Features.GameRounds.Commands;
using Location404.Game.Application.Services;
using Location404.Game.Domain.Entities;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Location404.Game.Application.UnitTests.Features.GameRounds.Commands;

public class SubmitGuessCommandHandlerTests
{
    private readonly IMatchRepository _matchRepository;
    private readonly IGuessRepository _guessRepository;
    private readonly IGameMatchManager _matchManager;
    private readonly IGuessStorageManager _guessStorage;
    private readonly IRoundTimerService _roundTimer;
    private readonly IGameEventPublisher _eventPublisher;
    private readonly IGeoDataClient _geoDataClient;
    private readonly ILogger<SubmitGuessCommandHandler> _logger;
    private readonly SubmitGuessCommandHandler _handler;

    public SubmitGuessCommandHandlerTests()
    {
        _matchRepository = Substitute.For<IMatchRepository>();
        _guessRepository = Substitute.For<IGuessRepository>();
        _matchManager = Substitute.For<IGameMatchManager>();
        _guessStorage = Substitute.For<IGuessStorageManager>();
        _roundTimer = Substitute.For<IRoundTimerService>();
        _eventPublisher = Substitute.For<IGameEventPublisher>();
        _geoDataClient = Substitute.For<IGeoDataClient>();
        _logger = Substitute.For<ILogger<SubmitGuessCommandHandler>>();

        _handler = new SubmitGuessCommandHandler(
            _matchRepository,
            _guessRepository,
            _matchManager,
            _guessStorage,
            _roundTimer,
            _eventPublisher,
            _geoDataClient,
            _logger
        );
    }

    [Fact]
    public async Task HandleAsync_WhenMatchNotFound_ShouldReturnNotFoundError()
    {
        // Arrange
        var command = new SubmitGuessCommand(
            MatchId: Guid.NewGuid(),
            PlayerId: Guid.NewGuid(),
            Guess: new Coordinate(-23.550520, -46.633308)
        );

        _matchManager.GetMatchAsync(command.MatchId)
            .Returns(Task.FromResult<GameMatch?>(null));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Match.NotFound");
        result.Error.Type.Should().Be(Common.Result.ErrorType.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenNoActiveRound_ShouldReturnValidationError()
    {
        // Arrange
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();

        var match = GameMatch.StartGameMatch(playerAId, playerBId);

        var command = new SubmitGuessCommand(
            MatchId: match.Id,
            PlayerId: playerAId,
            Guess: new Coordinate(-23.550520, -46.633308)
        );

        _matchManager.GetMatchAsync(match.Id).Returns(match);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Round.NotActive");
        result.Error.Type.Should().Be(Common.Result.ErrorType.Validation);
    }

    [Fact]
    public async Task HandleAsync_WhenFirstGuessSubmitted_ShouldStoreGuessAndReturnSuccess()
    {
        // Arrange
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();

        var match = GameMatch.StartGameMatch(playerAId, playerBId);
        match.StartNewGameRound();
        var roundId = match.CurrentGameRound!.Id;

        var playerAGuess = new Coordinate(-23.550000, -46.633000);

        var command = new SubmitGuessCommand(
            MatchId: match.Id,
            PlayerId: playerAId,
            Guess: playerAGuess
        );

        _matchManager.GetMatchAsync(match.Id).Returns(match);
        _guessStorage.GetBothGuessesAsync(match.Id, roundId, playerAId, playerBId)
            .Returns((playerAGuess, null));
        _roundTimer.GetRemainingTimeAsync(match.Id, roundId)
            .Returns(TimeSpan.FromSeconds(90));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RoundEnded.Should().BeFalse();
        result.Value.MatchEnded.Should().BeFalse();

        await _guessStorage.Received(1).StoreGuessAsync(match.Id, roundId, playerAId, playerAGuess);
        await _roundTimer.Received(1).AdjustTimerAsync(match.Id, roundId, TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task HandleAsync_WhenBothGuessesSubmitted_ShouldEndRoundAndReturnRoundResult()
    {
        // Arrange
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();

        var match = GameMatch.StartGameMatch(playerAId, playerBId);
        var correctLocation = new Coordinate(-23.550520, -46.633308);
        match.StartNewGameRound();
        var roundId = match.CurrentGameRound!.Id;

        var playerAGuess = new Coordinate(-23.550000, -46.633000);
        var playerBGuess = new Coordinate(-23.551000, -46.634000);

        var command = new SubmitGuessCommand(
            MatchId: match.Id,
            PlayerId: playerBId,
            Guess: playerBGuess
        );

        _matchManager.GetMatchAsync(match.Id).Returns(match);
        _guessStorage.GetBothGuessesAsync(match.Id, roundId, playerAId, playerBId)
            .Returns((playerAGuess, playerBGuess));
        _guessStorage.GetCorrectAnswerAsync(match.Id, roundId)
            .Returns(correctLocation);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RoundEnded.Should().BeTrue();
        result.Value.MatchEnded.Should().BeFalse();
        result.Value.RoundResult.Should().NotBeNull();
        result.Value.RoundResult!.CorrectLocation.Should().Be(correctLocation);
        result.Value.RoundResult.PlayerA.PlayerId.Should().Be(playerAId);
        result.Value.RoundResult.PlayerB.PlayerId.Should().Be(playerBId);

        await _roundTimer.Received(1).CancelTimerAsync(match.Id, roundId);
        await _guessStorage.Received(1).ClearGuessesAsync(match.Id, roundId);
        await _matchManager.Received(1).UpdateMatchAsync(Arg.Is<GameMatch>(m => m.GameRounds!.Count == 1));
    }

    [Fact]
    public async Task HandleAsync_WhenLastRoundCompleted_ShouldEndMatchAndReturnMatchResult()
    {
        // Arrange
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();

        var match = GameMatch.StartGameMatch(playerAId, playerBId);

        var locations = new[]
        {
            new Coordinate(-23.550520, -46.633308),
            new Coordinate(-22.906847, -43.172897),
            new Coordinate(-19.916681, -43.934493)
        };

        for (int i = 0; i < 3; i++)
        {
            match.StartNewGameRound();

            var playerAGuess = new Coordinate(locations[i].X + 0.01, locations[i].Y + 0.01);
            var playerBGuess = new Coordinate(locations[i].X - 0.01, locations[i].Y - 0.01);

            match.EndCurrentGameRound(locations[i], playerAGuess, playerBGuess);
        }

        match.StartNewGameRound();
        var finalRoundId = match.CurrentGameRound!.Id;

        var finalPlayerAGuess = new Coordinate(locations[2].X + 0.005, locations[2].Y + 0.005);
        var finalPlayerBGuess = new Coordinate(locations[2].X - 0.005, locations[2].Y - 0.005);

        var command = new SubmitGuessCommand(
            MatchId: match.Id,
            PlayerId: playerBId,
            Guess: finalPlayerBGuess
        );

        _matchManager.GetMatchAsync(match.Id).Returns(match);
        _guessStorage.GetBothGuessesAsync(match.Id, finalRoundId, playerAId, playerBId)
            .Returns((finalPlayerAGuess, finalPlayerBGuess));
        _guessStorage.GetCorrectAnswerAsync(match.Id, finalRoundId)
            .Returns(locations[2]);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RoundEnded.Should().BeTrue();
        result.Value.MatchEnded.Should().BeTrue();
        result.Value.RoundResult.Should().NotBeNull();
        result.Value.MatchResult.Should().NotBeNull();
        result.Value.MatchResult!.MatchId.Should().Be(match.Id);

        await _eventPublisher.Received(1).PublishMatchEndedAsync(Arg.Any<GameMatchEndedEvent>());
        await _matchManager.Received(1).RemoveMatchAsync(match.Id);
    }

    [Fact]
    public async Task HandleAsync_WhenCorrectAnswerNotFound_ShouldReturnNotFoundError()
    {
        // Arrange
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();

        var match = GameMatch.StartGameMatch(playerAId, playerBId);
        var location = new Coordinate(-23.550520, -46.633308);
        match.StartNewGameRound();
        var roundId = match.CurrentGameRound!.Id;

        var playerAGuess = new Coordinate(-23.550000, -46.633000);
        var playerBGuess = new Coordinate(-23.551000, -46.634000);

        var command = new SubmitGuessCommand(
            MatchId: match.Id,
            PlayerId: playerBId,
            Guess: playerBGuess
        );

        _matchManager.GetMatchAsync(match.Id).Returns(match);
        _guessStorage.GetBothGuessesAsync(match.Id, roundId, playerAId, playerBId)
            .Returns((playerAGuess, playerBGuess));
        _guessStorage.GetCorrectAnswerAsync(match.Id, roundId)
            .Returns(Task.FromResult<Coordinate?>(null));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Round.AnswerNotFound");
        result.Error.Type.Should().Be(Common.Result.ErrorType.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenSubmittingToNewRoundAfterPreviousEnded_ShouldSucceed()
    {
        // Arrange
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();

        var match = GameMatch.StartGameMatch(playerAId, playerBId);

        // First round - complete it
        match.StartNewGameRound();
        var oldRoundId = match.CurrentGameRound!.Id;
        var location1 = new Coordinate(-23.550520, -46.633308);
        var playerAGuess1 = new Coordinate(-23.550000, -46.633000);
        var playerBGuess1 = new Coordinate(-23.551000, -46.634000);
        match.EndCurrentGameRound(location1, playerAGuess1, playerBGuess1);

        // Second round - start new one
        match.StartNewGameRound();
        var newRoundId = match.CurrentGameRound!.Id;

        var playerBGuess2 = new Coordinate(-22.906000, -43.172000);
        var command = new SubmitGuessCommand(
            MatchId: match.Id,
            PlayerId: playerBId,
            Guess: playerBGuess2
        );

        _matchManager.GetMatchAsync(match.Id).Returns(match);
        _guessStorage.GetBothGuessesAsync(match.Id, newRoundId, playerAId, playerBId)
            .Returns((null, playerBGuess2));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RoundEnded.Should().BeFalse();
        result.Value.MatchEnded.Should().BeFalse();

        await _guessStorage.Received(1).StoreGuessAsync(match.Id, newRoundId, playerBId, playerBGuess2);
        await _guessStorage.DidNotReceive().GetCorrectAnswerAsync(Arg.Any<Guid>(), oldRoundId);
    }
}
