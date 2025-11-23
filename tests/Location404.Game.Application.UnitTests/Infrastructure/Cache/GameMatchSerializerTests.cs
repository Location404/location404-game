using Location404.Game.Domain.Entities;
using Location404.Game.Infrastructure.Cache;
using Xunit;

namespace Location404.Game.Application.UnitTests.Infrastructure.Cache;

public class GameMatchSerializerTests
{
    [Fact]
    public void Serialize_WithBasicMatch_ShouldSerializeCorrectly()
    {
        // Arrange
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var match = GameMatch.StartGameMatch(playerAId, playerBId);

        // Act
        var json = GameMatchSerializer.Serialize(match);

        // Assert
        Assert.NotNull(json);
        Assert.Contains(playerAId.ToString().ToLower(), json.ToLower());
        Assert.Contains(playerBId.ToString().ToLower(), json.ToLower());
    }

    [Fact]
    public void Deserialize_WithSerializedMatch_ShouldRestoreMatch()
    {
        // Arrange
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var match = GameMatch.StartGameMatch(playerAId, playerBId);
        var json = GameMatchSerializer.Serialize(match);

        // Act
        var deserializedMatch = GameMatchSerializer.Deserialize(json);

        // Assert
        Assert.NotNull(deserializedMatch);
        Assert.Equal(match.Id, deserializedMatch.Id);
        Assert.Equal(match.PlayerAId, deserializedMatch.PlayerAId);
        Assert.Equal(match.PlayerBId, deserializedMatch.PlayerBId);
        Assert.Equal(match.StartTime, deserializedMatch.StartTime);
    }

    [Fact]
    public void Serialize_WithCompletedRounds_ShouldIncludeRounds()
    {
        // Arrange
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var match = GameMatch.StartGameMatch(playerAId, playerBId);

        match.StartNewGameRound();
        var correctLocation = new Coordinate(x: -23.5505, y: -46.6333);
        var playerAGuess = new Coordinate(x: -23.5510, y: -46.6340);
        var playerBGuess = new Coordinate(x: -23.5500, y: -46.6330);
        match.EndCurrentGameRound(correctLocation, playerAGuess, playerBGuess);

        // Act
        var json = GameMatchSerializer.Serialize(match);

        // Assert
        Assert.Contains("gameRounds", json);
        Assert.Contains("-23.5505", json);
        Assert.Contains("-46.6333", json);
    }

    [Fact]
    public void Deserialize_WithCompletedRounds_ShouldRestoreRounds()
    {
        // Arrange
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var match = GameMatch.StartGameMatch(playerAId, playerBId);

        match.StartNewGameRound();
        var correctLocation = new Coordinate(x: -23.5505, y: -46.6333);
        var playerAGuess = new Coordinate(x: -23.5510, y: -46.6340);
        var playerBGuess = new Coordinate(x: -23.5500, y: -46.6330);
        match.EndCurrentGameRound(correctLocation, playerAGuess, playerBGuess);

        var json = GameMatchSerializer.Serialize(match);

        // Act
        var deserializedMatch = GameMatchSerializer.Deserialize(json);

        // Assert
        Assert.NotNull(deserializedMatch.GameRounds);
        Assert.Single(deserializedMatch.GameRounds);
        Assert.Equal(1, deserializedMatch.GameRounds[0].RoundNumber);
        Assert.True(deserializedMatch.GameRounds[0].GameRoundEnded);
        Assert.NotNull(deserializedMatch.GameRounds[0].GameResponse);
        Assert.Equal(-23.5505, deserializedMatch.GameRounds[0].GameResponse.X);
        Assert.Equal(-46.6333, deserializedMatch.GameRounds[0].GameResponse.Y);
    }

    [Fact]
    public void Serialize_WithCurrentGameRound_ShouldIncludeCurrentRound()
    {
        // Arrange
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var match = GameMatch.StartGameMatch(playerAId, playerBId);
        match.StartNewGameRound();

        // Act
        var json = GameMatchSerializer.Serialize(match);

        // Assert
        Assert.Contains("currentGameRound", json);
        Assert.Contains("roundNumber", json);
    }

    [Fact]
    public void Deserialize_WithCurrentGameRound_ShouldRestoreCurrentRound()
    {
        // Arrange
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var match = GameMatch.StartGameMatch(playerAId, playerBId);
        match.StartNewGameRound();
        var json = GameMatchSerializer.Serialize(match);

        // Act
        var deserializedMatch = GameMatchSerializer.Deserialize(json);

        // Assert
        Assert.NotNull(deserializedMatch.CurrentGameRound);
        Assert.Equal(1, deserializedMatch.CurrentGameRound.RoundNumber);
        Assert.Equal(playerAId, deserializedMatch.CurrentGameRound.PlayerAId);
        Assert.Equal(playerBId, deserializedMatch.CurrentGameRound.PlayerBId);
    }

    [Fact]
    public void Serialize_WithNullCoordinates_ShouldHandleNulls()
    {
        // Arrange
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var match = GameMatch.StartGameMatch(playerAId, playerBId);
        match.StartNewGameRound();

        // Act
        var json = GameMatchSerializer.Serialize(match);

        // Assert
        Assert.NotNull(json);
        var deserializedMatch = GameMatchSerializer.Deserialize(json);
        Assert.Null(deserializedMatch.CurrentGameRound?.GameResponse);
        Assert.Null(deserializedMatch.CurrentGameRound?.PlayerAGuess);
        Assert.Null(deserializedMatch.CurrentGameRound?.PlayerBGuess);
    }

    [Fact]
    public void Deserialize_WithInvalidJson_ShouldThrowException()
    {
        // Arrange
        var invalidJson = "{ invalid json }";

        // Act & Assert
        Assert.Throws<System.Text.Json.JsonException>(() =>
            GameMatchSerializer.Deserialize(invalidJson));
    }

    [Fact]
    public void Deserialize_WithNullJson_ShouldThrowException()
    {
        // Arrange
        var nullJson = "null";

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            GameMatchSerializer.Deserialize(nullJson));
    }

    [Fact]
    public void RoundTrip_ShouldProduceSameResult()
    {
        // Arrange
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var match = GameMatch.StartGameMatch(playerAId, playerBId);

        match.StartNewGameRound();
        var correctLocation = new Coordinate(x: -23.5505, y: -46.6333);
        var playerAGuess = new Coordinate(x: -23.5510, y: -46.6340);
        var playerBGuess = new Coordinate(x: -23.5500, y: -46.6330);
        match.EndCurrentGameRound(correctLocation, playerAGuess, playerBGuess);

        match.StartNewGameRound();

        // Act
        var json1 = GameMatchSerializer.Serialize(match);
        var deserializedMatch = GameMatchSerializer.Deserialize(json1);
        var json2 = GameMatchSerializer.Serialize(deserializedMatch);

        // Assert
        Assert.Equal(json1, json2);
    }

    [Fact]
    public void Deserialize_WithFinishedMatch_ShouldRestoreTotalPoints()
    {
        // Arrange
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var match = GameMatch.StartGameMatch(playerAId, playerBId);

        // Play 3 rounds with PlayerA winning
        for (int i = 0; i < 3; i++)
        {
            match.StartNewGameRound();
            var correctLocation = new Coordinate(x: -23.5505, y: -46.6333);
            var playerAGuess = new Coordinate(x: -23.5506, y: -46.6334); // Close
            var playerBGuess = new Coordinate(x: -25.0000, y: -48.0000); // Far
            match.EndCurrentGameRound(correctLocation, playerAGuess, playerBGuess);
        }

        var json = GameMatchSerializer.Serialize(match);

        // Act
        var deserializedMatch = GameMatchSerializer.Deserialize(json);

        // Assert
        Assert.Equal(3, deserializedMatch.GameRounds!.Count);
        Assert.Equal(match.PlayerATotalPoints, deserializedMatch.PlayerATotalPoints);
        Assert.Equal(match.PlayerBTotalPoints, deserializedMatch.PlayerBTotalPoints);
        Assert.True(deserializedMatch.PlayerATotalPoints > deserializedMatch.PlayerBTotalPoints);
    }

    [Fact]
    public void Serialize_WithMultipleRounds_ShouldPreserveOrder()
    {
        // Arrange
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var match = GameMatch.StartGameMatch(playerAId, playerBId);

        for (int i = 1; i <= 3; i++)
        {
            match.StartNewGameRound();
            var correctLocation = new Coordinate(x: -23.5505 + i, y: -46.6333 + i);
            var playerAGuess = new Coordinate(x: -23.5510 + i, y: -46.6340 + i);
            var playerBGuess = new Coordinate(x: -23.5500 + i, y: -46.6330 + i);
            match.EndCurrentGameRound(correctLocation, playerAGuess, playerBGuess);
        }

        var json = GameMatchSerializer.Serialize(match);

        // Act
        var deserializedMatch = GameMatchSerializer.Deserialize(json);

        // Assert
        Assert.Equal(3, deserializedMatch.GameRounds!.Count);
        Assert.Equal(1, deserializedMatch.GameRounds[0].RoundNumber);
        Assert.Equal(2, deserializedMatch.GameRounds[1].RoundNumber);
        Assert.Equal(3, deserializedMatch.GameRounds[2].RoundNumber);
    }

    [Fact]
    public void Deserialize_ShouldRestorePoints()
    {
        // Arrange
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var match = GameMatch.StartGameMatch(playerAId, playerBId);

        match.StartNewGameRound();
        var correctLocation = new Coordinate(x: -23.5505, y: -46.6333);
        var playerAGuess = new Coordinate(x: -23.5505, y: -46.6333); // Perfect guess
        var playerBGuess = new Coordinate(x: -25.0000, y: -48.0000); // Far
        match.EndCurrentGameRound(correctLocation, playerAGuess, playerBGuess);

        var json = GameMatchSerializer.Serialize(match);

        // Act
        var deserializedMatch = GameMatchSerializer.Deserialize(json);

        // Assert
        Assert.NotNull(deserializedMatch.GameRounds);
        Assert.Equal(5000, deserializedMatch.GameRounds[0].PlayerAPoints);
        Assert.NotNull(deserializedMatch.GameRounds[0].PlayerBPoints);
        Assert.True(deserializedMatch.GameRounds[0].PlayerAPoints > deserializedMatch.GameRounds[0].PlayerBPoints);
    }
}
