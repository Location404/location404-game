using Location404.Game.Domain.Entities;
using Location404.Game.Infrastructure.Cache;
using Xunit;

namespace Location404.Game.Application.UnitTests.Infrastructure.Cache;

public class InMemoryGuessStorageManagerTests
{
    [Fact]
    public async Task StoreGuessAsync_ShouldStoreGuess()
    {
        // Arrange
        var manager = new InMemoryGuessStorageManager();
        var matchId = Guid.NewGuid();
        var roundId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var guess = new Coordinate(x: -23.5505, y: -46.6333);

        // Act
        await manager.StoreGuessAsync(matchId, roundId, playerId, guess);

        // Assert
        var (playerA, _) = await manager.GetBothGuessesAsync(matchId, roundId, playerId, Guid.NewGuid());
        Assert.Equal(guess, playerA);
    }

    [Fact]
    public async Task GetBothGuessesAsync_WhenNoGuesses_ShouldReturnNulls()
    {
        // Arrange
        var manager = new InMemoryGuessStorageManager();
        var matchId = Guid.NewGuid();
        var roundId = Guid.NewGuid();
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();

        // Act
        var (guessA, guessB) = await manager.GetBothGuessesAsync(matchId, roundId, playerAId, playerBId);

        // Assert
        Assert.Null(guessA);
        Assert.Null(guessB);
    }

    [Fact]
    public async Task GetBothGuessesAsync_WhenBothPlayersGuessed_ShouldReturnBothGuesses()
    {
        // Arrange
        var manager = new InMemoryGuessStorageManager();
        var matchId = Guid.NewGuid();
        var roundId = Guid.NewGuid();
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var guessA = new Coordinate(x: -23.5505, y: -46.6333);
        var guessB = new Coordinate(x: -22.9068, y: -43.1729);

        await manager.StoreGuessAsync(matchId, roundId, playerAId, guessA);
        await manager.StoreGuessAsync(matchId, roundId, playerBId, guessB);

        // Act
        var (resultA, resultB) = await manager.GetBothGuessesAsync(matchId, roundId, playerAId, playerBId);

        // Assert
        Assert.Equal(guessA, resultA);
        Assert.Equal(guessB, resultB);
    }

    [Fact]
    public async Task GetBothGuessesAsync_WhenOnlyPlayerAGuessed_ShouldReturnOnlyPlayerAGuess()
    {
        // Arrange
        var manager = new InMemoryGuessStorageManager();
        var matchId = Guid.NewGuid();
        var roundId = Guid.NewGuid();
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var guessA = new Coordinate(x: -23.5505, y: -46.6333);

        await manager.StoreGuessAsync(matchId, roundId, playerAId, guessA);

        // Act
        var (resultA, resultB) = await manager.GetBothGuessesAsync(matchId, roundId, playerAId, playerBId);

        // Assert
        Assert.Equal(guessA, resultA);
        Assert.Null(resultB);
    }

    [Fact]
    public async Task ClearGuessesAsync_ShouldRemoveAllGuessesForRound()
    {
        // Arrange
        var manager = new InMemoryGuessStorageManager();
        var matchId = Guid.NewGuid();
        var roundId = Guid.NewGuid();
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var guessA = new Coordinate(x: -23.5505, y: -46.6333);
        var guessB = new Coordinate(x: -22.9068, y: -43.1729);

        await manager.StoreGuessAsync(matchId, roundId, playerAId, guessA);
        await manager.StoreGuessAsync(matchId, roundId, playerBId, guessB);

        // Act
        await manager.ClearGuessesAsync(matchId, roundId);

        // Assert
        var (resultA, resultB) = await manager.GetBothGuessesAsync(matchId, roundId, playerAId, playerBId);
        Assert.Null(resultA);
        Assert.Null(resultB);
    }

    [Fact]
    public async Task StoreCorrectAnswerAsync_ShouldStoreAnswer()
    {
        // Arrange
        var manager = new InMemoryGuessStorageManager();
        var matchId = Guid.NewGuid();
        var roundId = Guid.NewGuid();
        var correctAnswer = new Coordinate(x: -23.5505, y: -46.6333);

        // Act
        await manager.StoreCorrectAnswerAsync(matchId, roundId, correctAnswer);

        // Assert
        var result = await manager.GetCorrectAnswerAsync(matchId, roundId);
        Assert.Equal(correctAnswer, result);
    }

    [Fact]
    public async Task GetCorrectAnswerAsync_WhenNoAnswer_ShouldReturnNull()
    {
        // Arrange
        var manager = new InMemoryGuessStorageManager();
        var matchId = Guid.NewGuid();
        var roundId = Guid.NewGuid();

        // Act
        var result = await manager.GetCorrectAnswerAsync(matchId, roundId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ClearGuessesAsync_ShouldRemoveCorrectAnswer()
    {
        // Arrange
        var manager = new InMemoryGuessStorageManager();
        var matchId = Guid.NewGuid();
        var roundId = Guid.NewGuid();
        var correctAnswer = new Coordinate(x: -23.5505, y: -46.6333);

        await manager.StoreCorrectAnswerAsync(matchId, roundId, correctAnswer);

        // Act
        await manager.ClearGuessesAsync(matchId, roundId);

        // Assert
        var result = await manager.GetCorrectAnswerAsync(matchId, roundId);
        Assert.Null(result);
    }

    [Fact]
    public async Task ClearGuessesAsync_ShouldNotAffectOtherRounds()
    {
        // Arrange
        var manager = new InMemoryGuessStorageManager();
        var matchId = Guid.NewGuid();
        var round1Id = Guid.NewGuid();
        var round2Id = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var guess1 = new Coordinate(x: -23.5505, y: -46.6333);
        var guess2 = new Coordinate(x: -22.9068, y: -43.1729);

        await manager.StoreGuessAsync(matchId, round1Id, playerId, guess1);
        await manager.StoreGuessAsync(matchId, round2Id, playerId, guess2);

        // Act
        await manager.ClearGuessesAsync(matchId, round1Id);

        // Assert
        var (result1, _) = await manager.GetBothGuessesAsync(matchId, round1Id, playerId, Guid.NewGuid());
        var (result2, _) = await manager.GetBothGuessesAsync(matchId, round2Id, playerId, Guid.NewGuid());
        Assert.Null(result1);
        Assert.Equal(guess2, result2);
    }

    [Fact]
    public async Task StoreGuessAsync_WhenPlayerGuessesTwice_ShouldUpdateGuess()
    {
        // Arrange
        var manager = new InMemoryGuessStorageManager();
        var matchId = Guid.NewGuid();
        var roundId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var oldGuess = new Coordinate(x: -23.5505, y: -46.6333);
        var newGuess = new Coordinate(x: -22.9068, y: -43.1729);

        await manager.StoreGuessAsync(matchId, roundId, playerId, oldGuess);

        // Act
        await manager.StoreGuessAsync(matchId, roundId, playerId, newGuess);

        // Assert
        var (result, _) = await manager.GetBothGuessesAsync(matchId, roundId, playerId, Guid.NewGuid());
        Assert.Equal(newGuess, result);
    }

    [Fact]
    public async Task StoreCorrectAnswerAsync_WhenCalledTwice_ShouldUpdateAnswer()
    {
        // Arrange
        var manager = new InMemoryGuessStorageManager();
        var matchId = Guid.NewGuid();
        var roundId = Guid.NewGuid();
        var oldAnswer = new Coordinate(x: -23.5505, y: -46.6333);
        var newAnswer = new Coordinate(x: -22.9068, y: -43.1729);

        await manager.StoreCorrectAnswerAsync(matchId, roundId, oldAnswer);

        // Act
        await manager.StoreCorrectAnswerAsync(matchId, roundId, newAnswer);

        // Assert
        var result = await manager.GetCorrectAnswerAsync(matchId, roundId);
        Assert.Equal(newAnswer, result);
    }
}
