using FluentAssertions;
using Location404.Game.Application.Features.Matchmaking.Commands.JoinMatchmakingCommand;
using Location404.Game.Application.Common.Interfaces;
using Location404.Game.Application.Features.GameRounds.Interfaces;
using Location404.Game.Application.Features.Matchmaking.Interfaces;
using Location404.Game.Domain.Entities;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Location404.Game.Application.UnitTests.Features.Matchmaking.Commands;

public class JoinMatchmakingCommandHandlerTests
{
    private readonly IMatchmakingService _matchmaking;
    private readonly IGameMatchManager _matchManager;
    private readonly ILogger<JoinMatchmakingCommandHandler> _logger;
    private readonly JoinMatchmakingCommandHandler _handler;

    public JoinMatchmakingCommandHandlerTests()
    {
        _matchmaking = Substitute.For<IMatchmakingService>();
        _matchManager = Substitute.For<IGameMatchManager>();
        _logger = Substitute.For<ILogger<JoinMatchmakingCommandHandler>>();

        _handler = new JoinMatchmakingCommandHandler(
            _matchmaking,
            _matchManager,
            _logger
        );
    }

    [Fact]
    public async Task HandleAsync_WhenPlayerNotInMatch_ShouldJoinQueueSuccessfully()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var command = new JoinMatchmakingCommand(playerId);

        _matchManager.IsPlayerInMatchAsync(playerId).Returns(false);
        _matchmaking.TryFindMatchAsync().Returns(Task.FromResult<GameMatch?>(null));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MatchFound.Should().BeFalse();
        result.Value.Match.Should().BeNull();

        await _matchmaking.Received(1).JoinQueueAsync(playerId);
        await _matchmaking.Received(1).TryFindMatchAsync();
    }

    [Fact]
    public async Task HandleAsync_WhenMatchFoundImmediately_ShouldReturnMatch()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var opponentId = Guid.NewGuid();
        var command = new JoinMatchmakingCommand(playerId);

        var match = GameMatch.StartGameMatch(playerId, opponentId);

        _matchManager.IsPlayerInMatchAsync(playerId).Returns(false);
        _matchmaking.TryFindMatchAsync().Returns(match);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MatchFound.Should().BeTrue();
        result.Value.Match.Should().NotBeNull();
        result.Value.Match!.Id.Should().Be(match.Id);
        result.Value.Match.PlayerAId.Should().Be(playerId);
        result.Value.Match.PlayerBId.Should().Be(opponentId);

        await _matchmaking.Received(1).JoinQueueAsync(playerId);
        await _matchmaking.Received(1).TryFindMatchAsync();
    }

    [Fact]
    public async Task HandleAsync_WhenPlayerInActiveMatch_ShouldCleanupAndRejoin()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var opponentId = Guid.NewGuid();
        var command = new JoinMatchmakingCommand(playerId);

        var existingMatch = GameMatch.StartGameMatch(playerId, opponentId);
        existingMatch.StartNewGameRound();

        _matchManager.IsPlayerInMatchAsync(playerId).Returns(true);
        _matchManager.GetPlayerCurrentMatchAsync(playerId).Returns(existingMatch);
        _matchmaking.TryFindMatchAsync().Returns(Task.FromResult<GameMatch?>(null));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MatchFound.Should().BeFalse();

        await _matchManager.Received(1).UpdateMatchAsync(Arg.Is<GameMatch>(m => m.EndTime != default(DateTime)));
        await _matchManager.Received(1).RemoveMatchAsync(existingMatch.Id);
        await _matchmaking.Received(1).JoinQueueAsync(playerId);
    }

    [Fact]
    public async Task HandleAsync_WhenPlayerInEndedMatch_ShouldCleanupAndRejoin()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var opponentId = Guid.NewGuid();
        var command = new JoinMatchmakingCommand(playerId);

        var existingMatch = GameMatch.StartGameMatch(playerId, opponentId);

        for (int i = 0; i < 3; i++)
        {
            existingMatch.StartNewGameRound();
            var correctLocation = new Coordinate(-23.550520 + i, -46.633308 + i);
            var playerAGuess = new Coordinate(correctLocation.X + 0.01, correctLocation.Y + 0.01);
            var playerBGuess = new Coordinate(correctLocation.X - 0.01, correctLocation.Y - 0.01);
            existingMatch.EndCurrentGameRound(correctLocation, playerAGuess, playerBGuess);
        }

        existingMatch.EndGameMatch();

        _matchManager.IsPlayerInMatchAsync(playerId).Returns(true);
        _matchManager.GetPlayerCurrentMatchAsync(playerId).Returns(existingMatch);
        _matchmaking.TryFindMatchAsync().Returns(Task.FromResult<GameMatch?>(null));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MatchFound.Should().BeFalse();

        await _matchManager.DidNotReceive().UpdateMatchAsync(Arg.Any<GameMatch>());
        await _matchManager.Received(1).RemoveMatchAsync(existingMatch.Id);
        await _matchmaking.Received(1).JoinQueueAsync(playerId);
    }

    [Fact]
    public async Task HandleAsync_WhenPlayerInMatchButMatchNotFound_ShouldClearStaleState()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var command = new JoinMatchmakingCommand(playerId);

        _matchManager.IsPlayerInMatchAsync(playerId).Returns(true);
        _matchManager.GetPlayerCurrentMatchAsync(playerId).Returns(Task.FromResult<GameMatch?>(null));
        _matchmaking.TryFindMatchAsync().Returns(Task.FromResult<GameMatch?>(null));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MatchFound.Should().BeFalse();

        await _matchManager.Received(1).ClearPlayerMatchStateAsync(playerId);
        await _matchManager.DidNotReceive().UpdateMatchAsync(Arg.Any<GameMatch>());
        await _matchManager.DidNotReceive().RemoveMatchAsync(Arg.Any<Guid>());
        await _matchmaking.Received(1).JoinQueueAsync(playerId);
    }

    [Fact]
    public async Task HandleAsync_WhenExceptionOccurs_ShouldReturnFailure()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var command = new JoinMatchmakingCommand(playerId);

        _matchManager.IsPlayerInMatchAsync(playerId)
            .Returns<bool>(_ => throw new Exception("Redis connection failed"));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Matchmaking.Failed");
        result.Error.Type.Should().Be(Common.Result.ErrorType.Failure);
        result.Error.Message.Should().Contain("Redis connection failed");

        await _matchmaking.DidNotReceive().JoinQueueAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task HandleAsync_WhenMatchNotProperlyEnded_ShouldFinalizeAsInterrupted()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var opponentId = Guid.NewGuid();
        var command = new JoinMatchmakingCommand(playerId);

        var existingMatch = GameMatch.StartGameMatch(playerId, opponentId);
        existingMatch.StartNewGameRound();
        var correctLocation = new Coordinate(-23.550520, -46.633308);
        var playerAGuess = new Coordinate(-23.550000, -46.633000);
        var playerBGuess = new Coordinate(-23.551000, -46.634000);
        existingMatch.EndCurrentGameRound(correctLocation, playerAGuess, playerBGuess);

        _matchManager.IsPlayerInMatchAsync(playerId).Returns(true);
        _matchManager.GetPlayerCurrentMatchAsync(playerId).Returns(existingMatch);
        _matchmaking.TryFindMatchAsync().Returns(Task.FromResult<GameMatch?>(null));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await _matchManager.Received(1).UpdateMatchAsync(Arg.Is<GameMatch>(m =>
            m.EndTime != default(DateTime) &&
            m.GameRounds!.Count == 1
        ));
        await _matchManager.Received(1).RemoveMatchAsync(existingMatch.Id);
    }
}
