using FluentAssertions;
using Location404.Game.Application.Common.Interfaces;
using Location404.Game.Application.DTOs;
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

public class StartRoundCommandHandlerTests
{
    private readonly IGameMatchManager _matchManager;
    private readonly IGuessStorageManager _guessStorage;
    private readonly IRoundTimerService _roundTimer;
    private readonly IGeoDataClient _geoDataClient;
    private readonly ILogger<StartRoundCommandHandler> _logger;
    private readonly StartRoundCommandHandler _handler;

    public StartRoundCommandHandlerTests()
    {
        _matchManager = Substitute.For<IGameMatchManager>();
        _guessStorage = Substitute.For<IGuessStorageManager>();
        _roundTimer = Substitute.For<IRoundTimerService>();
        _geoDataClient = Substitute.For<IGeoDataClient>();
        _logger = Substitute.For<ILogger<StartRoundCommandHandler>>();

        _handler = new StartRoundCommandHandler(
            _matchManager,
            _guessStorage,
            _roundTimer,
            _geoDataClient,
            _logger
        );
    }

    [Fact]
    public async Task HandleAsync_WhenMatchNotFound_ShouldReturnNotFoundError()
    {
        // Arrange
        var command = new StartRoundCommand(Guid.NewGuid());

        _matchManager.GetMatchAsync(command.MatchId).Returns(Task.FromResult<GameMatch?>(null));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Match.NotFound");
        result.Error.Type.Should().Be(Common.Result.ErrorType.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenMatchReachedMaxRounds_ShouldReturnValidationError()
    {
        // Arrange
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var match = GameMatch.StartGameMatch(playerAId, playerBId);

        for (int i = 0; i < 3; i++)
        {
            match.StartNewGameRound();
            var location = new Coordinate(-23.550520 + i, -46.633308 + i);
            var playerAGuess = new Coordinate(location.X + 0.01, location.Y + 0.01);
            var playerBGuess = new Coordinate(location.X - 0.01, location.Y - 0.01);
            match.EndCurrentGameRound(location, playerAGuess, playerBGuess);
        }

        var command = new StartRoundCommand(match.Id);

        _matchManager.GetMatchAsync(match.Id).Returns(match);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Round.CannotStart");
        result.Error.Type.Should().Be(Common.Result.ErrorType.Validation);
    }

    [Fact]
    public async Task HandleAsync_WhenGeoDataServiceAvailable_ShouldStartRoundWithExternalLocation()
    {
        // Arrange
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var match = GameMatch.StartGameMatch(playerAId, playerBId);
        var command = new StartRoundCommand(match.Id);

        var locationDto = new LocationDto(
            Id: Guid.NewGuid(),
            Coordinate: new CoordinateDto(-23.550520, -46.633308),
            Name: "São Paulo",
            Country: "Brazil",
            Region: "SP",
            Heading: 180,
            Pitch: 5
        );

        _matchManager.GetMatchAsync(match.Id).Returns(match);
        _geoDataClient.GetRandomLocationAsync().Returns(locationDto);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MatchId.Should().Be(match.Id);
        result.Value.RoundNumber.Should().Be(1);
        result.Value.Location.X.Should().Be(-23.550520);
        result.Value.Location.Y.Should().Be(-46.633308);
        result.Value.Heading.Should().Be(180);
        result.Value.Pitch.Should().Be(5);
        result.Value.DurationSeconds.Should().Be(90);

        await _matchManager.Received(1).UpdateMatchAsync(Arg.Is<GameMatch>(m =>
            m.CurrentGameRound != null &&
            m.CurrentGameRound.RoundNumber == 1
        ));

        await _guessStorage.Received(1).StoreCorrectAnswerAsync(
            match.Id,
            Arg.Any<Guid>(),
            Arg.Is<Coordinate>(c => c.X == -23.550520 && c.Y == -46.633308)
        );

        await _roundTimer.Received(1).StartTimerAsync(
            match.Id,
            Arg.Any<Guid>(),
            TimeSpan.FromSeconds(90)
        );
    }

    [Fact]
    public async Task HandleAsync_WhenGeoDataServiceUnavailable_ShouldStartRoundWithFallbackLocation()
    {
        // Arrange
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var match = GameMatch.StartGameMatch(playerAId, playerBId);
        var command = new StartRoundCommand(match.Id);

        _matchManager.GetMatchAsync(match.Id).Returns(match);
        _geoDataClient.GetRandomLocationAsync().Returns(Task.FromResult<LocationDto?>(null));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MatchId.Should().Be(match.Id);
        result.Value.RoundNumber.Should().Be(1);
        result.Value.Location.Should().NotBeNull();
        result.Value.Heading.Should().BeInRange(0, 360);
        result.Value.Pitch.Should().BeInRange(-10, 10);
        result.Value.DurationSeconds.Should().Be(90);

        await _matchManager.Received(1).UpdateMatchAsync(Arg.Any<GameMatch>());
        await _guessStorage.Received(1).StoreCorrectAnswerAsync(
            match.Id,
            Arg.Any<Guid>(),
            Arg.Any<Coordinate>()
        );
        await _roundTimer.Received(1).StartTimerAsync(
            match.Id,
            Arg.Any<Guid>(),
            TimeSpan.FromSeconds(90)
        );
    }

    [Fact]
    public async Task HandleAsync_WhenStartingSecondRound_ShouldIncrementRoundNumber()
    {
        // Arrange
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var match = GameMatch.StartGameMatch(playerAId, playerBId);

        match.StartNewGameRound();
        var location1 = new Coordinate(-23.550520, -46.633308);
        var playerAGuess1 = new Coordinate(-23.550000, -46.633000);
        var playerBGuess1 = new Coordinate(-23.551000, -46.634000);
        match.EndCurrentGameRound(location1, playerAGuess1, playerBGuess1);

        var command = new StartRoundCommand(match.Id);

        var locationDto = new LocationDto(
            Id: Guid.NewGuid(),
            Coordinate: new CoordinateDto(-22.906847, -43.172897),
            Name: "Rio de Janeiro",
            Country: "Brazil",
            Region: "RJ",
            Heading: 90,
            Pitch: 0
        );

        _matchManager.GetMatchAsync(match.Id).Returns(match);
        _geoDataClient.GetRandomLocationAsync().Returns(locationDto);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RoundNumber.Should().Be(2);
        result.Value.Location.X.Should().Be(-22.906847);
        result.Value.Location.Y.Should().Be(-43.172897);

        await _matchManager.Received(1).UpdateMatchAsync(Arg.Any<GameMatch>());
    }

    [Fact]
    public async Task HandleAsync_WhenHeadingAndPitchNull_ShouldGenerateRandomValues()
    {
        // Arrange
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var match = GameMatch.StartGameMatch(playerAId, playerBId);
        var command = new StartRoundCommand(match.Id);

        var locationDto = new LocationDto(
            Id: Guid.NewGuid(),
            Coordinate: new CoordinateDto(-23.550520, -46.633308),
            Name: "São Paulo",
            Country: "Brazil",
            Region: "SP",
            Heading: null,
            Pitch: null
        );

        _matchManager.GetMatchAsync(match.Id).Returns(match);
        _geoDataClient.GetRandomLocationAsync().Returns(locationDto);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Heading.Should().BeInRange(0, 360);
        result.Value.Pitch.Should().BeInRange(-10, 10);
    }

    [Fact]
    public async Task HandleAsync_WhenExceptionOccurs_ShouldReturnFailure()
    {
        // Arrange
        var command = new StartRoundCommand(Guid.NewGuid());

        _matchManager.GetMatchAsync(command.MatchId)
            .Returns<GameMatch?>(_ => throw new Exception("Redis connection failed"));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StartRound.Failed");
        result.Error.Type.Should().Be(Common.Result.ErrorType.Failure);
        result.Error.Message.Should().Contain("Redis connection failed");
    }
}
