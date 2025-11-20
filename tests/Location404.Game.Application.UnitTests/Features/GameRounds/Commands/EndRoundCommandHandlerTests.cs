using FluentAssertions;

using Location404.Game.Application.Common.Interfaces;
using Location404.Game.Application.Features.GameRounds.Commands.SubmitGuessCommand;
using Location404.Game.Application.Features.GameRounds.Commands.EndRoundCommand;
using Location404.Game.Application.Features.GameRounds.Commands.StartRoundCommand;
using Location404.Game.Application.Common.Interfaces;
using Location404.Game.Application.Features.GameRounds.Interfaces;
using Location404.Game.Application.Features.Matchmaking.Interfaces;
using Location404.Game.Domain.Entities;

using Microsoft.Extensions.Logging;

using NSubstitute;

namespace Location404.Game.Application.UnitTests.Features.GameRounds.Commands;

public class EndRoundCommandHandlerTests
{
    private readonly IGameMatchManager _matchManager;
    private readonly IGuessStorageManager _guessStorage;
    private readonly IRoundTimerService _roundTimer;
    private readonly IGameEventPublisher _eventPublisher;
    private readonly ILocation404DataClient _location404DataClient;
    private readonly ILogger<EndRoundCommandHandler> _logger;
    private readonly EndRoundCommandHandler _handler;

    public EndRoundCommandHandlerTests()
    {
        _matchManager = Substitute.For<IGameMatchManager>();
        _guessStorage = Substitute.For<IGuessStorageManager>();
        _roundTimer = Substitute.For<IRoundTimerService>();
        _eventPublisher = Substitute.For<IGameEventPublisher>();
        _location404DataClient = Substitute.For<ILocation404DataClient>();
        _logger = Substitute.For<ILogger<EndRoundCommandHandler>>();

        _handler = new EndRoundCommandHandler(
            _matchManager,
            _guessStorage,
            _roundTimer,
            _eventPublisher,
            _location404DataClient,
            _logger
        );
    }

    [Fact]
    public async Task HandleAsync_WhenMatchNotFound_ShouldReturnNotFoundError()
    {
        // Arrange
        var command = new EndRoundCommand(
            MatchId: Guid.NewGuid(),
            RoundId: Guid.NewGuid(),
            PlayerAGuess: new Coordinate(-23.550000, -46.633000),
            PlayerBGuess: new Coordinate(-23.551000, -46.634000)
        );

        _matchManager.GetMatchAsync(command.MatchId).Returns(Task.FromResult<GameMatch?>(null));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Match.NotFound");
        result.Error.Type.Should().Be(Common.Result.ErrorType.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenRoundAlreadyEnded_ShouldReturnSuccessWithoutProcessing()
    {
        // Arrange
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var match = GameMatch.StartGameMatch(playerAId, playerBId);

        match.StartNewGameRound();
        var oldRoundId = match.CurrentGameRound!.Id;
        var location1 = new Coordinate(-23.550520, -46.633308);
        var playerAGuess1 = new Coordinate(-23.550000, -46.633000);
        var playerBGuess1 = new Coordinate(-23.551000, -46.634000);
        match.EndCurrentGameRound(location1, playerAGuess1, playerBGuess1);

        var command = new EndRoundCommand(
            MatchId: match.Id,
            RoundId: oldRoundId,
            PlayerAGuess: playerAGuess1,
            PlayerBGuess: playerBGuess1
        );

        _matchManager.GetMatchAsync(match.Id).Returns(match);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RoundEnded.Should().BeTrue();
        result.Value.MatchEnded.Should().BeFalse();
        result.Value.RoundResult.Should().BeNull();

        await _roundTimer.Received(1).CancelTimerAsync(command.MatchId, command.RoundId);
        await _guessStorage.DidNotReceive().GetCorrectAnswerAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Fact]
    public async Task HandleAsync_WhenCorrectAnswerNotFound_ShouldReturnNotFoundError()
    {
        // Arrange
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var match = GameMatch.StartGameMatch(playerAId, playerBId);
        match.StartNewGameRound();
        var roundId = match.CurrentGameRound!.Id;

        var command = new EndRoundCommand(
            MatchId: match.Id,
            RoundId: roundId,
            PlayerAGuess: new Coordinate(-23.550000, -46.633000),
            PlayerBGuess: new Coordinate(-23.551000, -46.634000)
        );

        _matchManager.GetMatchAsync(match.Id).Returns(match);
        _guessStorage.GetCorrectAnswerAsync(match.Id, roundId).Returns(Task.FromResult<Coordinate?>(null));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Round.AnswerNotFound");
        result.Error.Type.Should().Be(Common.Result.ErrorType.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenFirstRoundEnds_ShouldReturnRoundResultAndNotEndMatch()
    {
        // Arrange
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var match = GameMatch.StartGameMatch(playerAId, playerBId);
        match.StartNewGameRound();
        var roundId = match.CurrentGameRound!.Id;

        var correctLocation = new Coordinate(-23.550520, -46.633308);
        var playerAGuess = new Coordinate(-23.550000, -46.633000);
        var playerBGuess = new Coordinate(-23.551000, -46.634000);

        var command = new EndRoundCommand(
            MatchId: match.Id,
            RoundId: roundId,
            PlayerAGuess: playerAGuess,
            PlayerBGuess: playerBGuess
        );

        _matchManager.GetMatchAsync(match.Id).Returns(match);
        _guessStorage.GetCorrectAnswerAsync(match.Id, roundId).Returns(correctLocation);

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
        result.Value.MatchResult.Should().BeNull();

        await _roundTimer.Received(1).CancelTimerAsync(match.Id, roundId);
        await _guessStorage.Received(1).ClearGuessesAsync(match.Id, roundId);
        await _matchManager.Received(1).UpdateMatchAsync(Arg.Any<GameMatch>());
        await _eventPublisher.Received(1).PublishRoundEndedAsync(Arg.Any<Application.Events.GameRoundEndedEvent>());
        await _matchManager.DidNotReceive().RemoveMatchAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task HandleAsync_WhenLastRoundEnds_ShouldEndMatchAndPublishEvents()
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

        for (int i = 0; i < 2; i++)
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

        var command = new EndRoundCommand(
            MatchId: match.Id,
            RoundId: finalRoundId,
            PlayerAGuess: finalPlayerAGuess,
            PlayerBGuess: finalPlayerBGuess
        );

        _matchManager.GetMatchAsync(match.Id).Returns(match);
        _guessStorage.GetCorrectAnswerAsync(match.Id, finalRoundId).Returns(locations[2]);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RoundEnded.Should().BeTrue();
        result.Value.MatchEnded.Should().BeTrue();
        result.Value.RoundResult.Should().NotBeNull();
        result.Value.MatchResult.Should().NotBeNull();
        result.Value.MatchResult!.MatchId.Should().Be(match.Id);

        await _eventPublisher.Received(1).PublishRoundEndedAsync(Arg.Any<Application.Events.GameRoundEndedEvent>());
        await _eventPublisher.Received(1).PublishMatchEndedAsync(Arg.Any<Application.Events.GameMatchEndedEvent>());
        await _matchManager.Received(1).RemoveMatchAsync(match.Id);
    }

    [Fact]
    public async Task HandleAsync_WhenRabbitMQFails_ShouldUseHTTPFallback()
    {
        // Arrange
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var match = GameMatch.StartGameMatch(playerAId, playerBId);

        for (int i = 0; i < 2; i++)
        {
            match.StartNewGameRound();
            var location = new Coordinate(-23.550520 + i, -46.633308 + i);
            var playerAGuess = new Coordinate(location.X + 0.01, location.Y + 0.01);
            var playerBGuess = new Coordinate(location.X - 0.01, location.Y - 0.01);
            match.EndCurrentGameRound(location, playerAGuess, playerBGuess);
        }

        match.StartNewGameRound();
        var finalRoundId = match.CurrentGameRound!.Id;
        var finalLocation = new Coordinate(-19.916681, -43.934493);
        var finalPlayerAGuess = new Coordinate(finalLocation.X + 0.005, finalLocation.Y + 0.005);
        var finalPlayerBGuess = new Coordinate(finalLocation.X - 0.005, finalLocation.Y - 0.005);

        var command = new EndRoundCommand(
            MatchId: match.Id,
            RoundId: finalRoundId,
            PlayerAGuess: finalPlayerAGuess,
            PlayerBGuess: finalPlayerBGuess
        );

        _matchManager.GetMatchAsync(match.Id).Returns(match);
        _guessStorage.GetCorrectAnswerAsync(match.Id, finalRoundId).Returns(finalLocation);
        _eventPublisher.PublishMatchEndedAsync(Arg.Any<Application.Events.GameMatchEndedEvent>())
            .Returns<Task>(_ => throw new Exception("RabbitMQ connection failed"));
        _location404DataClient.SendMatchEndedAsync(Arg.Any<Application.Events.GameMatchEndedEvent>())
            .Returns(true);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MatchEnded.Should().BeTrue();

        await _eventPublisher.Received(1).PublishMatchEndedAsync(Arg.Any<Application.Events.GameMatchEndedEvent>());

        await Task.Delay(100);
    }

    [Fact]
    public async Task HandleAsync_WhenExceptionOccurs_ShouldReturnFailure()
    {
        // Arrange
        var command = new EndRoundCommand(
            MatchId: Guid.NewGuid(),
            RoundId: Guid.NewGuid(),
            PlayerAGuess: new Coordinate(-23.550000, -46.633000),
            PlayerBGuess: new Coordinate(-23.551000, -46.634000)
        );

        _matchManager.GetMatchAsync(command.MatchId)
            .Returns<GameMatch?>(_ => throw new Exception("Redis connection failed"));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("EndRound.Failed");
        result.Error.Type.Should().Be(Common.Result.ErrorType.Failure);
        result.Error.Message.Should().Contain("Redis connection failed");
    }
}
