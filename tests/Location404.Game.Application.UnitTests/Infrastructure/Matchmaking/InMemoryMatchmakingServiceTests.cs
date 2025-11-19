namespace Location404.Game.Application.UnitTests.Infrastructure.Matchmaking;

using FluentAssertions;
using Location404.Game.Application.Common.Interfaces;
using Location404.Game.Application.Features.GameRounds.Interfaces;
using Location404.Game.Application.Features.Matchmaking.Interfaces;
using Location404.Game.Domain.Entities;
using Location404.Game.Infrastructure.Matchmaking;
using NSubstitute;

public class InMemoryMatchmakingServiceTests
{
    private readonly IGameMatchManager _mockMatchManager;
    private readonly InMemoryMatchmakingService _sut;

    public InMemoryMatchmakingServiceTests()
    {
        _mockMatchManager = Substitute.For<IGameMatchManager>();
        _sut = new InMemoryMatchmakingService(_mockMatchManager);
    }

    [Fact]
    public async Task JoinQueueAsync_WhenPlayerNotInMatch_ShouldAddToQueue()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        _mockMatchManager.IsPlayerInMatchAsync(playerId).Returns(false);

        // Act
        var result = await _sut.JoinQueueAsync(playerId);

        // Assert
        result.Should().Be(playerId);
        (await _sut.IsPlayerInQueueAsync(playerId)).Should().BeTrue();
        (await _sut.GetQueueSizeAsync()).Should().Be(1);
    }

    [Fact]
    public async Task JoinQueueAsync_WhenPlayerAlreadyInMatch_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        _mockMatchManager.IsPlayerInMatchAsync(playerId).Returns(true);

        // Act
        var act = async () => await _sut.JoinQueueAsync(playerId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Player is already in a match.");
    }

    [Fact]
    public async Task JoinQueueAsync_WhenPlayerAlreadyInQueue_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        _mockMatchManager.IsPlayerInMatchAsync(playerId).Returns(false);
        await _sut.JoinQueueAsync(playerId);

        // Act
        var act = async () => await _sut.JoinQueueAsync(playerId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Player is already in queue.");
    }

    [Fact]
    public async Task JoinQueueAsync_WhenMultiplePlayersJoinSimultaneously_ShouldHandleAllPlayers()
    {
        // Arrange
        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();
        var player3 = Guid.NewGuid();
        _mockMatchManager.IsPlayerInMatchAsync(Arg.Any<Guid>()).Returns(false);

        // Act - Simulate concurrent joins
        var tasks = new[]
        {
            _sut.JoinQueueAsync(player1),
            _sut.JoinQueueAsync(player2),
            _sut.JoinQueueAsync(player3)
        };
        await Task.WhenAll(tasks);

        // Assert
        (await _sut.GetQueueSizeAsync()).Should().Be(3);
        (await _sut.IsPlayerInQueueAsync(player1)).Should().BeTrue();
        (await _sut.IsPlayerInQueueAsync(player2)).Should().BeTrue();
        (await _sut.IsPlayerInQueueAsync(player3)).Should().BeTrue();
    }

    [Fact]
    public async Task LeaveQueueAsync_WhenPlayerInQueue_ShouldRemoveFromQueue()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        _mockMatchManager.IsPlayerInMatchAsync(playerId).Returns(false);
        await _sut.JoinQueueAsync(playerId);

        // Act
        await _sut.LeaveQueueAsync(playerId);

        // Assert
        (await _sut.IsPlayerInQueueAsync(playerId)).Should().BeFalse();
        (await _sut.GetQueueSizeAsync()).Should().Be(0);
    }

    [Fact]
    public async Task LeaveQueueAsync_WhenPlayerNotInQueue_ShouldNotThrow()
    {
        // Arrange
        var playerId = Guid.NewGuid();

        // Act
        var act = async () => await _sut.LeaveQueueAsync(playerId);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task TryFindMatchAsync_WhenQueueHasLessThanTwoPlayers_ShouldReturnNull()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        _mockMatchManager.IsPlayerInMatchAsync(playerId).Returns(false);
        await _sut.JoinQueueAsync(playerId);

        // Act
        var match = await _sut.TryFindMatchAsync();

        // Assert
        match.Should().BeNull();
        (await _sut.GetQueueSizeAsync()).Should().Be(1);
    }

    [Fact]
    public async Task TryFindMatchAsync_WhenQueueHasTwoPlayers_ShouldCreateMatchAndRemoveFromQueue()
    {
        // Arrange
        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();
        var expectedMatch = GameMatch.StartGameMatch(player1, player2);

        _mockMatchManager.IsPlayerInMatchAsync(Arg.Any<Guid>()).Returns(false);
        _mockMatchManager.CreateMatchAsync(player1, player2).Returns(expectedMatch);

        await _sut.JoinQueueAsync(player1);
        await Task.Delay(10); // Ensure different timestamps
        await _sut.JoinQueueAsync(player2);

        // Act
        var match = await _sut.TryFindMatchAsync();

        // Assert
        match.Should().NotBeNull();
        match.Should().Be(expectedMatch);
        (await _sut.GetQueueSizeAsync()).Should().Be(0);
        (await _sut.IsPlayerInQueueAsync(player1)).Should().BeFalse();
        (await _sut.IsPlayerInQueueAsync(player2)).Should().BeFalse();
    }

    [Fact]
    public async Task TryFindMatchAsync_WhenQueueHasMoreThanTwoPlayers_ShouldMatchFirstTwoByFIFO()
    {
        // Arrange
        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();
        var player3 = Guid.NewGuid();
        var expectedMatch = GameMatch.StartGameMatch(player1, player2);

        _mockMatchManager.IsPlayerInMatchAsync(Arg.Any<Guid>()).Returns(false);
        _mockMatchManager.CreateMatchAsync(player1, player2).Returns(expectedMatch);

        await _sut.JoinQueueAsync(player1);
        await Task.Delay(10);
        await _sut.JoinQueueAsync(player2);
        await Task.Delay(10);
        await _sut.JoinQueueAsync(player3);

        // Act
        var match = await _sut.TryFindMatchAsync();

        // Assert
        match.Should().NotBeNull();
        match!.PlayerAId.Should().Be(player1);
        match.PlayerBId.Should().Be(player2);
        (await _sut.GetQueueSizeAsync()).Should().Be(1);
        (await _sut.IsPlayerInQueueAsync(player3)).Should().BeTrue();
    }

    [Fact]
    public async Task GetQueueSizeAsync_WhenQueueEmpty_ShouldReturnZero()
    {
        // Act
        var size = await _sut.GetQueueSizeAsync();

        // Assert
        size.Should().Be(0);
    }

    [Fact]
    public async Task IsPlayerInQueueAsync_WhenPlayerNotInQueue_ShouldReturnFalse()
    {
        // Arrange
        var playerId = Guid.NewGuid();

        // Act
        var isInQueue = await _sut.IsPlayerInQueueAsync(playerId);

        // Assert
        isInQueue.Should().BeFalse();
    }

    [Fact]
    public async Task TryFindMatchAsync_ShouldHandleConcurrentMatchmaking()
    {
        // Arrange
        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();
        var player3 = Guid.NewGuid();
        var player4 = Guid.NewGuid();

        _mockMatchManager.IsPlayerInMatchAsync(Arg.Any<Guid>()).Returns(false);
        _mockMatchManager.CreateMatchAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns(call => GameMatch.StartGameMatch(call.ArgAt<Guid>(0), call.ArgAt<Guid>(1)));

        // Act - Add 4 players concurrently
        await Task.WhenAll(
            _sut.JoinQueueAsync(player1),
            _sut.JoinQueueAsync(player2),
            _sut.JoinQueueAsync(player3),
            _sut.JoinQueueAsync(player4)
        );

        // Try to match concurrently
        var matchTasks = new[]
        {
            _sut.TryFindMatchAsync(),
            _sut.TryFindMatchAsync()
        };
        var matches = await Task.WhenAll(matchTasks);

        // Assert
        var successfulMatches = matches.Where(m => m != null).ToList();
        successfulMatches.Should().HaveCount(2);
        (await _sut.GetQueueSizeAsync()).Should().Be(0);
    }
}
