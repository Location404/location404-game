using Location404.Game.Application.DTOs;
using Location404.Game.Application.Features.GameRounds;
using Location404.Game.Application.Features.GameRounds.Commands.SubmitGuessCommand;
using Location404.Game.Domain.Entities;
using Xunit;

namespace Location404.Game.Application.UnitTests.Features.GameRounds;

public class RoundEndedResponseTests
{
    [Fact]
    public void FromGameRound_ShouldMapAllPropertiesCorrectly()
    {
        // Arrange
        var matchId = Guid.NewGuid();
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var round = GameRound.StartGameRound(matchId, 1, playerAId, playerBId);

        var gameResponse = new Coordinate(x: -23.5505, y: -46.6333);
        var playerAGuess = new Coordinate(x: -23.5510, y: -46.6340);
        var playerBGuess = new Coordinate(x: -23.5500, y: -46.6330);

        round.EndGameRound(gameResponse, playerAGuess, playerBGuess);

        var playerATotalPoints = 5000;
        var playerBTotalPoints = 4500;

        // Act
        var response = RoundEndedResponse.FromGameRound(round, playerATotalPoints, playerBTotalPoints);

        // Assert
        Assert.Equal(matchId, response.MatchId);
        Assert.Equal(round.Id, response.RoundId);
        Assert.Equal(1, response.RoundNumber);
        Assert.NotNull(response.CorrectAnswer);
        Assert.Equal(gameResponse.X, response.CorrectAnswer.X);
        Assert.Equal(gameResponse.Y, response.CorrectAnswer.Y);
        Assert.NotNull(response.PlayerAGuess);
        Assert.NotNull(response.PlayerBGuess);
        Assert.Equal(playerATotalPoints, response.PlayerATotalPoints);
        Assert.Equal(playerBTotalPoints, response.PlayerBTotalPoints);
    }

    [Fact]
    public void FromGameRound_WithNullGuesses_ShouldReturnNullForGuesses()
    {
        // Arrange
        var round = GameRound.StartGameRound(Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid());
        var gameResponse = new Coordinate(x: -23.5505, y: -46.6333);

        round.EndGameRound(gameResponse, null, null);

        // Act
        var response = RoundEndedResponse.FromGameRound(round, 0, 0);

        // Assert
        Assert.Null(response.PlayerAGuess);
        Assert.Null(response.PlayerBGuess);
        Assert.Equal(0, response.PlayerAPoints);
        Assert.Equal(0, response.PlayerBPoints);
    }

    [Fact]
    public void FromGameRound_WhenRoundNotEnded_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var round = GameRound.StartGameRound(Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            RoundEndedResponse.FromGameRound(round, 0, 0));
        Assert.Equal("Round must be ended before creating response.", exception.Message);
    }

    [Fact]
    public void FromGameRound_WithPlayerAWinner_ShouldSetCorrectWinnerId()
    {
        // Arrange
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var round = GameRound.StartGameRound(Guid.NewGuid(), 1, playerAId, playerBId);

        var gameResponse = new Coordinate(x: -23.5505, y: -46.6333);
        var playerAGuess = new Coordinate(x: -23.5510, y: -46.6340); // Close
        var playerBGuess = new Coordinate(x: -25.0000, y: -48.0000); // Far

        round.EndGameRound(gameResponse, playerAGuess, playerBGuess);

        // Act
        var response = RoundEndedResponse.FromGameRound(round, 5000, 4000);

        // Assert
        Assert.Equal(playerAId, response.RoundWinnerId);
    }

    [Fact]
    public void FromGameRound_WithDraw_ShouldHaveNullWinnerId()
    {
        // Arrange
        var round = GameRound.StartGameRound(Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid());

        var gameResponse = new Coordinate(x: -23.5505, y: -46.6333);
        var sameGuess = new Coordinate(x: -23.5510, y: -46.6340);

        round.EndGameRound(gameResponse, sameGuess, sameGuess);

        // Act
        var response = RoundEndedResponse.FromGameRound(round, 5000, 5000);

        // Assert
        Assert.Null(response.RoundWinnerId);
    }

    [Fact]
    public void FromRoundEndResult_ShouldMapAllPropertiesCorrectly()
    {
        // Arrange
        var roundId = Guid.NewGuid();
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var correctLocation = new Coordinate(x: -23.5505, y: -46.6333);
        var playerAGuess = new Coordinate(x: -23.5510, y: -46.6340);
        var playerBGuess = new Coordinate(x: -23.5500, y: -46.6330);

        var result = new RoundEndResult(
            RoundId: roundId,
            RoundNumber: 2,
            CorrectLocation: correctLocation,
            PlayerA: new PlayerGuessResult(playerAId, playerAGuess, 4800, 100.5),
            PlayerB: new PlayerGuessResult(playerBId, playerBGuess, 4900, 95.3),
            PlayerATotalPoints: 9800,
            PlayerBTotalPoints: 9900
        );

        // Act
        var response = RoundEndedResponse.FromRoundEndResult(result);

        // Assert
        Assert.Equal(roundId, response.RoundId);
        Assert.Equal(2, response.RoundNumber);
        Assert.NotNull(response.CorrectAnswer);
        Assert.Equal(correctLocation.X, response.CorrectAnswer.X);
        Assert.Equal(correctLocation.Y, response.CorrectAnswer.Y);
        Assert.Equal(4800, response.PlayerAPoints);
        Assert.Equal(4900, response.PlayerBPoints);
        Assert.Equal(9800, response.PlayerATotalPoints);
        Assert.Equal(9900, response.PlayerBTotalPoints);
    }

    [Fact]
    public void FromRoundEndResult_WithPlayerBWinner_ShouldSetCorrectWinnerId()
    {
        // Arrange
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var result = new RoundEndResult(
            RoundId: Guid.NewGuid(),
            RoundNumber: 1,
            CorrectLocation: new Coordinate(x: 0, y: 0),
            PlayerA: new PlayerGuessResult(playerAId, new Coordinate(x: 0, y: 0), 3000, 500.0),
            PlayerB: new PlayerGuessResult(playerBId, new Coordinate(x: 0, y: 0), 4000, 300.0),
            PlayerATotalPoints: 3000,
            PlayerBTotalPoints: 4000
        );

        // Act
        var response = RoundEndedResponse.FromRoundEndResult(result);

        // Assert
        Assert.Equal(playerBId, response.RoundWinnerId);
    }

    [Fact]
    public void FromRoundEndResult_WithDraw_ShouldHaveNullWinnerId()
    {
        // Arrange
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var result = new RoundEndResult(
            RoundId: Guid.NewGuid(),
            RoundNumber: 1,
            CorrectLocation: new Coordinate(x: 0, y: 0),
            PlayerA: new PlayerGuessResult(playerAId, new Coordinate(x: 0, y: 0), 5000, 0.0),
            PlayerB: new PlayerGuessResult(playerBId, new Coordinate(x: 0, y: 0), 5000, 0.0),
            PlayerATotalPoints: 5000,
            PlayerBTotalPoints: 5000
        );

        // Act
        var response = RoundEndedResponse.FromRoundEndResult(result);

        // Assert
        Assert.Null(response.RoundWinnerId);
    }

    [Fact]
    public void FromRoundEndResult_ShouldSetMatchIdToEmpty()
    {
        // Arrange
        var result = new RoundEndResult(
            RoundId: Guid.NewGuid(),
            RoundNumber: 1,
            CorrectLocation: new Coordinate(x: 0, y: 0),
            PlayerA: new PlayerGuessResult(Guid.NewGuid(), new Coordinate(x: 0, y: 0), 5000, 0.0),
            PlayerB: new PlayerGuessResult(Guid.NewGuid(), new Coordinate(x: 0, y: 0), 5000, 0.0),
            PlayerATotalPoints: 5000,
            PlayerBTotalPoints: 5000
        );

        // Act
        var response = RoundEndedResponse.FromRoundEndResult(result);

        // Assert
        Assert.Equal(Guid.Empty, response.MatchId);
    }
}
