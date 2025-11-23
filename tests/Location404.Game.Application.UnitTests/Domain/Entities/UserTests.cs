using Location404.Game.Domain.Entities;
using Xunit;

namespace Location404.Game.Application.UnitTests.Domain.Entities;

public class UserTests
{
    [Fact]
    public void Constructor_ShouldSetAllPropertiesCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var username = "testuser";
        var email = "test@example.com";
        var profileImage = new byte[] { 1, 2, 3 };

        // Act
        var user = new User(id, username, email, profileImage);

        // Assert
        Assert.Equal(id, user.Id);
        Assert.Equal(username, user.Username);
        Assert.Equal(email, user.Email);
        Assert.Equal(profileImage, user.ProfileImage);
        Assert.Empty(user.HistoryGameMatch);
        Assert.Null(user.CurrentGameMatch);
        Assert.Equal(0, user.TotalPoints);
    }

    [Fact]
    public void Constructor_WithNullProfileImage_ShouldAcceptNull()
    {
        // Arrange
        var id = Guid.NewGuid();
        var username = "testuser";
        var email = "test@example.com";

        // Act
        var user = new User(id, username, email);

        // Assert
        Assert.Null(user.ProfileImage);
    }

    [Fact]
    public void Constructor_WithHistoryAndCurrentMatch_ShouldSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var history = new List<GameMatch>
        {
            GameMatch.StartGameMatch(playerAId, playerBId)
        };
        var currentMatch = GameMatch.StartGameMatch(id, playerBId);

        // Act
        var user = new User(id, "user", "user@test.com", null, history, currentMatch, 100);

        // Assert
        Assert.Single(user.HistoryGameMatch);
        Assert.NotNull(user.CurrentGameMatch);
        Assert.Equal(100, user.TotalPoints);
    }

    [Fact]
    public void IsInGameMatch_WhenCurrentMatchIsNull_ShouldReturnFalse()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), "user", "user@test.com");

        // Act
        var isInGame = user.IsInGameMatch();

        // Assert
        Assert.False(isInGame);
    }

    [Fact]
    public void IsInGameMatch_WhenCurrentMatchExists_ShouldReturnTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentMatch = GameMatch.StartGameMatch(userId, Guid.NewGuid());
        var user = new User(userId, "user", "user@test.com", null, null, currentMatch);

        // Act
        var isInGame = user.IsInGameMatch();

        // Assert
        Assert.True(isInGame);
    }

    [Fact]
    public void StartGameMatch_WhenNotInMatch_ShouldSetCurrentMatch()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User(userId, "user", "user@test.com");
        var gameMatch = GameMatch.StartGameMatch(userId, Guid.NewGuid());

        // Act
        user.StartGameMatch(gameMatch);

        // Assert
        Assert.NotNull(user.CurrentGameMatch);
        Assert.Equal(gameMatch, user.CurrentGameMatch);
        Assert.True(user.IsInGameMatch());
    }

    [Fact]
    public void StartGameMatch_WhenAlreadyInMatch_ShouldThrowException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingMatch = GameMatch.StartGameMatch(userId, Guid.NewGuid());
        var user = new User(userId, "user", "user@test.com", null, null, existingMatch);
        var newMatch = GameMatch.StartGameMatch(userId, Guid.NewGuid());

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => user.StartGameMatch(newMatch));
        Assert.Equal("User is already in a game match.", exception.Message);
    }

    [Fact]
    public void SearchHistoryGameResults_WithNoHistory_ShouldReturnEmptyArray()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), "user", "user@test.com");

        // Act
        var results = user.SearchHistoryGameResults();

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void SearchHistoryGameResults_WithOneMatch_ShouldReturnOneResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var opponentId = Guid.NewGuid();
        var match = new GameMatch(
            Guid.NewGuid(),
            userId,
            opponentId,
            DateTime.UtcNow.AddHours(-1),
            DateTime.UtcNow,
            userId,
            opponentId,
            100,
            50
        );
        var history = new List<GameMatch> { match };
        var user = new User(userId, "user", "user@test.com", null, history);

        // Act
        var results = user.SearchHistoryGameResults();

        // Assert
        Assert.Single(results);
        Assert.True(results[0]);
    }

    [Fact]
    public void SearchHistoryGameResults_WithMultipleMatches_ShouldReturnLast3()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var opponentId = Guid.NewGuid();
        var history = new List<GameMatch>();

        for (int i = 0; i < 5; i++)
        {
            var winnerId = i % 2 == 0 ? userId : opponentId;
            var loserId = i % 2 == 0 ? opponentId : userId;
            var match = new GameMatch(
                Guid.NewGuid(),
                userId,
                opponentId,
                DateTime.UtcNow.AddHours(-5 + i),
                DateTime.UtcNow.AddHours(-4 + i),
                winnerId,
                loserId,
                100,
                50
            );
            history.Add(match);
        }

        var user = new User(userId, "user", "user@test.com", null, history);

        // Act
        var results = user.SearchHistoryGameResults();

        // Assert
        Assert.Equal(3, results.Length);
    }

    [Fact]
    public void SearchHistoryGameResults_ShouldOrderByMostRecentFirst()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var opponentId = Guid.NewGuid();

        var oldMatch = new GameMatch(
            Guid.NewGuid(),
            userId,
            opponentId,
            DateTime.UtcNow.AddDays(-3),
            DateTime.UtcNow.AddDays(-2),
            userId,
            opponentId,
            100,
            50
        );

        var recentMatch = new GameMatch(
            Guid.NewGuid(),
            userId,
            opponentId,
            DateTime.UtcNow.AddHours(-1),
            DateTime.UtcNow,
            opponentId,
            userId,
            100,
            50
        );

        var history = new List<GameMatch> { oldMatch, recentMatch };
        var user = new User(userId, "user", "user@test.com", null, history);

        // Act
        var results = user.SearchHistoryGameResults();

        // Assert
        Assert.Equal(2, results.Length);
        Assert.False(results[0]);
        Assert.True(results[1]);
    }

    [Fact]
    public void SearchHistoryGameResults_WhenUserWon_ShouldReturnTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var opponentId = Guid.NewGuid();
        var match = new GameMatch(
            Guid.NewGuid(),
            userId,
            opponentId,
            DateTime.UtcNow.AddHours(-1),
            DateTime.UtcNow,
            userId,
            opponentId,
            100,
            50
        );
        var history = new List<GameMatch> { match };
        var user = new User(userId, "user", "user@test.com", null, history);

        // Act
        var results = user.SearchHistoryGameResults();

        // Assert
        Assert.Single(results);
        Assert.True(results[0]);
    }

    [Fact]
    public void SearchHistoryGameResults_WhenUserLost_ShouldReturnFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var opponentId = Guid.NewGuid();
        var match = new GameMatch(
            Guid.NewGuid(),
            userId,
            opponentId,
            DateTime.UtcNow.AddHours(-1),
            DateTime.UtcNow,
            opponentId,
            userId,
            100,
            50
        );
        var history = new List<GameMatch> { match };
        var user = new User(userId, "user", "user@test.com", null, history);

        // Act
        var results = user.SearchHistoryGameResults();

        // Assert
        Assert.Single(results);
        Assert.False(results[0]);
    }

    [Fact]
    public void HistoryGameResults_ShouldBeCachedProperty()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User(userId, "user", "user@test.com");

        // Act
        var results1 = user.HistoryGameResults;
        var results2 = user.HistoryGameResults;

        // Assert
        Assert.NotSame(results1, results2);
    }

    [Fact]
    public void TotalPoints_ShouldBeInitializedToZero()
    {
        // Arrange & Act
        var user = new User(Guid.NewGuid(), "user", "user@test.com");

        // Assert
        Assert.Equal(0, user.TotalPoints);
    }

    [Fact]
    public void TotalPoints_ShouldPersistCustomValue()
    {
        // Arrange & Act
        var user = new User(Guid.NewGuid(), "user", "user@test.com", null, null, null, 500);

        // Assert
        Assert.Equal(500, user.TotalPoints);
    }
}
