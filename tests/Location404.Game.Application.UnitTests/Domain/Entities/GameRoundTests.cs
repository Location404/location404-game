using Location404.Game.Domain.Entities;
using Xunit;

namespace Location404.Game.Application.UnitTests.Domain.Entities;

public class GameRoundTests
{
    [Fact]
    public void StartGameRound_ShouldCreateRoundWithCorrectInitialState()
    {
        // Arrange
        var gameMatchId = Guid.NewGuid();
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var roundNumber = 1;

        // Act
        var round = GameRound.StartGameRound(gameMatchId, roundNumber, playerAId, playerBId);

        // Assert
        Assert.NotEqual(Guid.Empty, round.Id);
        Assert.Equal(gameMatchId, round.GameMatchId);
        Assert.Equal(roundNumber, round.RoundNumber);
        Assert.Equal(playerAId, round.PlayerAId);
        Assert.Equal(playerBId, round.PlayerBId);
        Assert.Null(round.PlayerAPoints);
        Assert.Null(round.PlayerBPoints);
        Assert.Null(round.GameResponse);
        Assert.Null(round.PlayerAGuess);
        Assert.Null(round.PlayerBGuess);
        Assert.False(round.GameRoundEnded);
    }

    [Fact]
    public void EndGameRound_ShouldSetAllPropertiesCorrectly()
    {
        // Arrange
        var round = GameRound.StartGameRound(Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid());
        var gameResponse = new Coordinate(x: -23.5505, y: -46.6333);
        var playerAGuess = new Coordinate(x: -23.5510, y: -46.6340);
        var playerBGuess = new Coordinate(x: -23.5500, y: -46.6330);

        // Act
        round.EndGameRound(gameResponse, playerAGuess, playerBGuess);

        // Assert
        Assert.Equal(gameResponse, round.GameResponse);
        Assert.Equal(playerAGuess, round.PlayerAGuess);
        Assert.Equal(playerBGuess, round.PlayerBGuess);
        Assert.NotNull(round.PlayerAPoints);
        Assert.NotNull(round.PlayerBPoints);
        Assert.True(round.GameRoundEnded);
    }

    [Fact]
    public void EndGameRound_WithNullGuesses_ShouldGiveZeroPoints()
    {
        // Arrange
        var round = GameRound.StartGameRound(Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid());
        var gameResponse = new Coordinate(x: -23.5505, y: -46.6333);

        // Act
        round.EndGameRound(gameResponse, null, null);

        // Assert
        Assert.Equal(0, round.PlayerAPoints);
        Assert.Equal(0, round.PlayerBPoints);
        Assert.True(round.GameRoundEnded);
    }

    [Fact]
    public void EndGameRound_WithPerfectGuess_ShouldGiveMaxPoints()
    {
        // Arrange
        var round = GameRound.StartGameRound(Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid());
        var gameResponse = new Coordinate(x: -23.5505, y: -46.6333);
        var perfectGuess = new Coordinate(x: -23.5505, y: -46.6333);

        // Act
        round.EndGameRound(gameResponse, perfectGuess, perfectGuess);

        // Assert
        Assert.Equal(5000, round.PlayerAPoints);
        Assert.Equal(5000, round.PlayerBPoints);
    }

    [Fact]
    public void EndGameRound_WithCloseGuess_ShouldGiveHighPoints()
    {
        // Arrange
        var round = GameRound.StartGameRound(Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid());
        var gameResponse = new Coordinate(x: -23.5505, y: -46.6333);
        var closeGuess = new Coordinate(x: -23.5510, y: -46.6340);

        // Act
        round.EndGameRound(gameResponse, closeGuess, null);

        // Assert
        Assert.NotNull(round.PlayerAPoints);
        Assert.InRange(round.PlayerAPoints.Value, 4900, 5000);
        Assert.Equal(0, round.PlayerBPoints);
    }

    [Fact]
    public void EndGameRound_WithFarGuess_ShouldGiveLowPoints()
    {
        // Arrange
        var round = GameRound.StartGameRound(Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid());
        var gameResponse = new Coordinate(x: -23.5505, y: -46.6333);
        var farGuess = new Coordinate(x: 40.7128, y: -74.0060);

        // Act
        round.EndGameRound(gameResponse, farGuess, null);

        // Assert
        Assert.NotNull(round.PlayerAPoints);
        Assert.InRange(round.PlayerAPoints.Value, 0, 100);
    }

    [Fact]
    public void EndGameRound_WithVeryFarGuess_ShouldGiveNearZeroPoints()
    {
        // Arrange
        var round = GameRound.StartGameRound(Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid());
        var gameResponse = new Coordinate(x: -23.5505, y: -46.6333);
        var veryFarGuess = new Coordinate(x: 51.5074, y: -0.1278);

        // Act
        round.EndGameRound(gameResponse, veryFarGuess, null);

        // Assert
        Assert.NotNull(round.PlayerAPoints);
        Assert.InRange(round.PlayerAPoints.Value, 0, 50);
    }

    [Theory]
    [InlineData(0, 5000)]
    [InlineData(100, 4756)]
    [InlineData(500, 3894)]
    [InlineData(1000, 3033)]
    [InlineData(2000, 1839)]
    public void EndGameRound_ScoringFormula_ShouldFollowExponentialDecay(double distanceKm, int expectedPoints)
    {
        // Arrange
        var round = GameRound.StartGameRound(Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid());
        var gameResponse = new Coordinate(x: 0, y: 0);

        var distanceInDegrees = distanceKm / 111.0;
        var playerGuess = new Coordinate(x: distanceInDegrees, y: 0);

        // Act
        round.EndGameRound(gameResponse, playerGuess, null);

        // Assert
        Assert.NotNull(round.PlayerAPoints);
        Assert.InRange(round.PlayerAPoints.Value, expectedPoints - 100, expectedPoints + 100);
    }

    [Fact]
    public void PlayerWinner_WhenPlayerAWins_ShouldReturnPlayerAId()
    {
        // Arrange
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var round = GameRound.StartGameRound(Guid.NewGuid(), 1, playerAId, playerBId);
        var gameResponse = new Coordinate(x: -23.5505, y: -46.6333);
        var playerAGuess = new Coordinate(x: -23.5510, y: -46.6340);
        var playerBGuess = new Coordinate(x: -25.0000, y: -48.0000);

        round.EndGameRound(gameResponse, playerAGuess, playerBGuess);

        // Act
        var winner = round.PlayerWinner();

        // Assert
        Assert.Equal(playerAId, winner);
    }

    [Fact]
    public void PlayerWinner_WhenPlayerBWins_ShouldReturnPlayerBId()
    {
        // Arrange
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var round = GameRound.StartGameRound(Guid.NewGuid(), 1, playerAId, playerBId);
        var gameResponse = new Coordinate(x: -23.5505, y: -46.6333);
        var playerAGuess = new Coordinate(x: -25.0000, y: -48.0000);
        var playerBGuess = new Coordinate(x: -23.5510, y: -46.6340);

        round.EndGameRound(gameResponse, playerAGuess, playerBGuess);

        // Act
        var winner = round.PlayerWinner();

        // Assert
        Assert.Equal(playerBId, winner);
    }

    [Fact]
    public void PlayerWinner_WhenDraw_ShouldReturnNull()
    {
        // Arrange
        var round = GameRound.StartGameRound(Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid());
        var gameResponse = new Coordinate(x: -23.5505, y: -46.6333);
        var sameGuess = new Coordinate(x: -23.5510, y: -46.6340);

        round.EndGameRound(gameResponse, sameGuess, sameGuess);

        // Act
        var winner = round.PlayerWinner();

        // Assert
        Assert.Null(winner);
    }

    [Fact]
    public void PlayerWinner_WhenRoundNotEnded_ShouldThrowException()
    {
        // Arrange
        var round = GameRound.StartGameRound(Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => round.PlayerWinner());
        Assert.Equal("Round has not ended or points are not calculated.", exception.Message);
    }

    [Fact]
    public void EndGameRound_WithMixedGuesses_ShouldCalculateIndependently()
    {
        // Arrange
        var round = GameRound.StartGameRound(Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid());
        var gameResponse = new Coordinate(x: -23.5505, y: -46.6333);
        var playerAGuess = new Coordinate(x: -23.5510, y: -46.6340);

        // Act
        round.EndGameRound(gameResponse, playerAGuess, null);

        // Assert
        Assert.NotNull(round.PlayerAPoints);
        Assert.True(round.PlayerAPoints > 0);
        Assert.Equal(0, round.PlayerBPoints);
    }

    [Fact]
    public void RoundNumber_ShouldPersistCorrectly()
    {
        // Arrange & Act
        var round1 = GameRound.StartGameRound(Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid());
        var round2 = GameRound.StartGameRound(Guid.NewGuid(), 2, Guid.NewGuid(), Guid.NewGuid());
        var round3 = GameRound.StartGameRound(Guid.NewGuid(), 3, Guid.NewGuid(), Guid.NewGuid());

        // Assert
        Assert.Equal(1, round1.RoundNumber);
        Assert.Equal(2, round2.RoundNumber);
        Assert.Equal(3, round3.RoundNumber);
    }
}
