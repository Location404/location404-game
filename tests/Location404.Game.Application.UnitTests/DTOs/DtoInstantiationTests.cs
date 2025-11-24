using Location404.Game.Application.DTOs;
using Location404.Game.Application.DTOs.Integration;
using Location404.Game.Application.Features.GameRounds;
using Location404.Game.Application.Features.GameRounds.Commands.EndRoundCommand;
using Location404.Game.Application.Features.GameRounds.Commands.StartRoundCommand;
using Location404.Game.Application.Features.GameRounds.Commands.SubmitGuessCommand;
using Location404.Game.Application.Features.Matchmaking.Commands.JoinMatchmakingCommand;
using Location404.Game.Domain.Entities;
using Xunit;

namespace Location404.Game.Application.UnitTests.DTOs;

public class DtoInstantiationTests
{
    [Fact]
    public void UpdateUserStatsRequest_ShouldInstantiateCorrectly()
    {
        var userId = Guid.NewGuid();
        var request = new UpdateUserStatsRequest(userId, 100, true);

        Assert.Equal(userId, request.UserId);
        Assert.Equal(100, request.PointsChange);
        Assert.True(request.IsWinner);
    }

    [Fact]
    public void UserDto_ShouldInstantiateCorrectly()
    {
        var userId = Guid.NewGuid();
        var dto = new UserDto(userId, "TestUser", "test@example.com", 1500, false);

        Assert.Equal(userId, dto.Id);
        Assert.Equal("TestUser", dto.Username);
        Assert.Equal("test@example.com", dto.Email);
        Assert.Equal(1500, dto.TotalPoints);
        Assert.False(dto.IsInGameMatch);
    }

    [Fact]
    public void EndRoundRequest_ShouldInstantiateCorrectly()
    {
        var matchId = Guid.NewGuid();
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var request = new EndRoundRequest(
            matchId,
            -23.5505, -46.6333,
            playerAId, -23.5500, -46.6330,
            playerBId, -23.5510, -46.6340
        );

        Assert.Equal(matchId, request.MatchId);
        Assert.Equal(playerAId, request.PlayerAId);
        Assert.Equal(playerBId, request.PlayerBId);
    }

    [Fact]
    public void EndRoundRequest_GetGameResponse_ShouldReturnCoordinate()
    {
        var request = new EndRoundRequest(
            Guid.NewGuid(),
            -23.5505, -46.6333,
            Guid.NewGuid(), -23.5500, -46.6330,
            Guid.NewGuid(), -23.5510, -46.6340
        );

        var coordinate = request.GetGameResponse();

        Assert.Equal(-23.5505, coordinate.X);
        Assert.Equal(-46.6333, coordinate.Y);
    }

    [Fact]
    public void EndRoundRequest_GetPlayerAGuess_ShouldReturnCoordinate()
    {
        var request = new EndRoundRequest(
            Guid.NewGuid(),
            -23.5505, -46.6333,
            Guid.NewGuid(), -23.5500, -46.6330,
            Guid.NewGuid(), -23.5510, -46.6340
        );

        var coordinate = request.GetPlayerAGuess();

        Assert.Equal(-23.5500, coordinate.X);
        Assert.Equal(-46.6330, coordinate.Y);
    }

    [Fact]
    public void EndRoundRequest_GetPlayerBGuess_ShouldReturnCoordinate()
    {
        var request = new EndRoundRequest(
            Guid.NewGuid(),
            -23.5505, -46.6333,
            Guid.NewGuid(), -23.5500, -46.6330,
            Guid.NewGuid(), -23.5510, -46.6340
        );

        var coordinate = request.GetPlayerBGuess();

        Assert.Equal(-23.5510, coordinate.X);
        Assert.Equal(-46.6340, coordinate.Y);
    }

    [Fact]
    public void StartRoundRequest_ShouldInstantiateCorrectly()
    {
        var matchId = Guid.NewGuid();
        var request = new StartRoundRequest(matchId);

        Assert.Equal(matchId, request.MatchId);
    }

    [Fact]
    public void SubmitGuessRequest_ShouldInstantiateCorrectly()
    {
        var matchId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var request = new SubmitGuessRequest(matchId, playerId, -23.5505, -46.6333);

        Assert.Equal(matchId, request.MatchId);
        Assert.Equal(playerId, request.PlayerId);
        Assert.Equal(-23.5505, request.X);
        Assert.Equal(-46.6333, request.Y);
    }

    [Fact]
    public void SubmitGuessRequest_ToCoordinate_ShouldReturnCoordinate()
    {
        var request = new SubmitGuessRequest(Guid.NewGuid(), Guid.NewGuid(), -23.5505, -46.6333);

        var coordinate = request.ToCoordinate();

        Assert.Equal(-23.5505, coordinate.X);
        Assert.Equal(-46.6333, coordinate.Y);
    }

    [Fact]
    public void JoinMatchmakingRequest_ShouldInstantiateCorrectly()
    {
        var playerId = Guid.NewGuid();
        var request = new JoinMatchmakingRequest(playerId);

        Assert.Equal(playerId, request.PlayerId);
    }

    [Fact]
    public void RoundStartedResponse_ShouldInstantiateCorrectly()
    {
        var roundId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var startTime = DateTime.UtcNow;
        var locationData = new LocationData(-23.5505, -46.6333, 10.5, 15.3);
        var startedAt = DateTimeOffset.UtcNow;

        var response = new RoundStartedResponse(
            MatchId: matchId,
            RoundId: roundId,
            RoundNumber: 1,
            StartTime: startTime,
            Location: locationData,
            StartedAt: startedAt,
            DurationSeconds: 60
        );

        Assert.Equal(roundId, response.RoundId);
        Assert.Equal(1, response.RoundNumber);
        Assert.Equal(matchId, response.MatchId);
        Assert.Equal(locationData, response.Location);
        Assert.Equal(60, response.DurationSeconds);
    }

    [Fact]
    public void LocationData_ShouldInstantiateCorrectly()
    {
        var locationData = new LocationData(-23.5505, -46.6333, 10.5, 15.3);

        Assert.Equal(-23.5505, locationData.X);
        Assert.Equal(-46.6333, locationData.Y);
        Assert.Equal(10.5, locationData.Heading);
        Assert.Equal(15.3, locationData.Pitch);
    }

    [Fact]
    public void LocationData_ToCoordinate_ShouldConvertCorrectly()
    {
        var locationData = new LocationData(-23.5505, -46.6333, 10.5, 15.3);

        var coordinate = locationData.ToCoordinate();

        Assert.Equal(-23.5505, coordinate.X);
        Assert.Equal(-46.6333, coordinate.Y);
    }

    [Fact]
    public void MatchFoundResponse_ShouldInstantiateCorrectly()
    {
        var matchId = Guid.NewGuid();
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();
        var startTime = DateTime.UtcNow;

        var response = new MatchFoundResponse(matchId, playerAId, playerBId, startTime);

        Assert.Equal(matchId, response.MatchId);
        Assert.Equal(playerAId, response.PlayerAId);
        Assert.Equal(playerBId, response.PlayerBId);
        Assert.Equal(startTime, response.StartTime);
    }

    [Fact]
    public void MatchEndResult_ShouldInstantiateCorrectly()
    {
        var matchId = Guid.NewGuid();
        var winnerId = Guid.NewGuid();
        var loserId = Guid.NewGuid();
        var rounds = new List<GameRound>();

        var result = new MatchEndResult(
            matchId,
            winnerId,
            loserId,
            15000,
            12000,
            24,
            0,
            rounds
        );

        Assert.Equal(matchId, result.MatchId);
        Assert.Equal(winnerId, result.WinnerId);
        Assert.Equal(loserId, result.LoserId);
        Assert.Equal(15000, result.PlayerAFinalPoints);
        Assert.Equal(12000, result.PlayerBFinalPoints);
        Assert.Equal(24, result.PointsEarned);
        Assert.Equal(0, result.PointsLost);
        Assert.Equal(rounds, result.Rounds);
    }
}
