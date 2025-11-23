using Location404.Game.Domain.Entities;
using Location404.Game.Infrastructure.Cache;
using Xunit;

namespace Location404.Game.Infrastructure.IntegrationTests.Cache;

public class GuessStorageManagerTests : IClassFixture<RedisFixture>
{
    private readonly RedisFixture _fixture;

    public GuessStorageManagerTests(RedisFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task StoreGuessAsync_ShouldStoreGuess()
    {
        // Arrange
        var manager = new GuessStorageManager(_fixture.Redis);
        var matchId = Guid.NewGuid();
        var roundId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var guess = new Coordinate(x: -23.5505, y: -46.6333);

        // Act
        await manager.StoreGuessAsync(matchId, roundId, playerId, guess);

        // Assert
        var (playerA, _) = await manager.GetBothGuessesAsync(matchId, roundId, playerId, Guid.NewGuid());
        Assert.NotNull(playerA);
        Assert.Equal(guess.X, playerA.X);
        Assert.Equal(guess.Y, playerA.Y);

        // Cleanup
        await manager.ClearGuessesAsync(matchId, roundId);
    }

    [Fact]
    public async Task GetBothGuessesAsync_WhenNoGuesses_ShouldReturnNulls()
    {
        // Arrange
        var manager = new GuessStorageManager(_fixture.Redis);
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
        var manager = new GuessStorageManager(_fixture.Redis);
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
        Assert.NotNull(resultA);
        Assert.NotNull(resultB);
        Assert.Equal(guessA.X, resultA.X);
        Assert.Equal(guessA.Y, resultA.Y);
        Assert.Equal(guessB.X, resultB.X);
        Assert.Equal(guessB.Y, resultB.Y);

        // Cleanup
        await manager.ClearGuessesAsync(matchId, roundId);
    }

    [Fact]
    public async Task ClearGuessesAsync_ShouldRemoveAllGuessesForRound()
    {
        // Arrange
        var manager = new GuessStorageManager(_fixture.Redis);
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
        var manager = new GuessStorageManager(_fixture.Redis);
        var matchId = Guid.NewGuid();
        var roundId = Guid.NewGuid();
        var correctAnswer = new Coordinate(x: -23.5505, y: -46.6333);

        // Act
        await manager.StoreCorrectAnswerAsync(matchId, roundId, correctAnswer);

        // Assert
        var result = await manager.GetCorrectAnswerAsync(matchId, roundId);
        Assert.NotNull(result);
        Assert.Equal(correctAnswer.X, result.X);
        Assert.Equal(correctAnswer.Y, result.Y);

        // Cleanup
        await manager.ClearGuessesAsync(matchId, roundId);
    }

    [Fact]
    public async Task GetCorrectAnswerAsync_WhenNoAnswer_ShouldReturnNull()
    {
        // Arrange
        var manager = new GuessStorageManager(_fixture.Redis);
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
        var manager = new GuessStorageManager(_fixture.Redis);
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
}
