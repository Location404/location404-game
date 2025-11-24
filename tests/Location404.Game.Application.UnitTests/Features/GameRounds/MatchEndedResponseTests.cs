using Location404.Game.Application.DTOs;
using Location404.Game.Application.Features.GameRounds;
using Location404.Game.Application.Features.GameRounds.Commands.SubmitGuessCommand;
using Location404.Game.Domain.Entities;
using Xunit;

namespace Location404.Game.Application.UnitTests.Features.GameRounds;

public class MatchEndedResponseTests
{
    [Fact]
    public void FromGameMatch_WithCompleteMatch_ShouldCreateResponse()
    {
        var match = GameMatch.StartGameMatch(Guid.NewGuid(), Guid.NewGuid());
        var correctLocation = new Coordinate(-23.5505, -46.6333);
        var playerAGuess = new Coordinate(-23.5500, -46.6330);
        var playerBGuess = new Coordinate(-23.5510, -46.6340);

        for (int i = 0; i < 3; i++)
        {
            match.StartNewGameRound();
            match.EndCurrentGameRound(correctLocation, playerAGuess, playerBGuess);
        }

        match.EndGameMatch();

        var response = MatchEndedResponse.FromGameMatch(match);

        Assert.Equal(match.Id, response.MatchId);
        Assert.Equal(match.PlayerWinnerId, response.WinnerId);
        Assert.Equal(match.PlayerLoserId, response.LoserId);
        Assert.Equal(match.PlayerATotalPoints, response.PlayerATotalPoints);
        Assert.Equal(match.PlayerBTotalPoints, response.PlayerBTotalPoints);
        Assert.Equal(match.PointsEarned, response.PointsEarned);
        Assert.Equal(match.PointsLost, response.PointsLost);
        Assert.NotEqual(DateTime.MinValue, response.EndTime);
        Assert.Equal(3, response.Rounds.Count);
    }

    [Fact]
    public void FromGameMatch_WithoutRounds_ShouldThrowInvalidOperationException()
    {
        var match = GameMatch.StartGameMatch(Guid.NewGuid(), Guid.NewGuid());

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MatchEndedResponse.FromGameMatch(match)
        );

        Assert.Equal("Match must have rounds before creating response.", exception.Message);
    }

    [Fact]
    public void FromMatchEndResult_WithValidResult_ShouldCreateResponse()
    {
        var matchId = Guid.NewGuid();
        var winnerId = Guid.NewGuid();
        var loserId = Guid.NewGuid();

        var match = GameMatch.StartGameMatch(Guid.NewGuid(), Guid.NewGuid());
        var correctLocation = new Coordinate(-23.5505, -46.6333);
        var playerAGuess = new Coordinate(-23.5500, -46.6330);
        var playerBGuess = new Coordinate(-23.5510, -46.6340);

        match.StartNewGameRound();
        match.EndCurrentGameRound(correctLocation, playerAGuess, playerBGuess);

        var rounds = new List<GameRound> { match.GameRounds!.First() };

        var matchEndResult = new MatchEndResult(
            matchId,
            winnerId,
            loserId,
            15000,
            12000,
            24,
            0,
            rounds
        );

        var response = MatchEndedResponse.FromMatchEndResult(matchEndResult);

        Assert.Equal(matchId, response.MatchId);
        Assert.Equal(winnerId, response.WinnerId);
        Assert.Equal(loserId, response.LoserId);
        Assert.Equal(15000, response.PlayerATotalPoints);
        Assert.Equal(12000, response.PlayerBTotalPoints);
        Assert.Equal(24, response.PointsEarned);
        Assert.Equal(0, response.PointsLost);
        Assert.Single(response.Rounds);
    }

    [Fact]
    public void FromMatchEndResult_WithEmptyWinnerId_ShouldReturnNullWinner()
    {
        var matchId = Guid.NewGuid();

        var match = GameMatch.StartGameMatch(Guid.NewGuid(), Guid.NewGuid());
        var correctLocation = new Coordinate(-23.5505, -46.6333);
        var playerAGuess = new Coordinate(-23.5500, -46.6330);
        var playerBGuess = new Coordinate(-23.5510, -46.6340);

        match.StartNewGameRound();
        match.EndCurrentGameRound(correctLocation, playerAGuess, playerBGuess);

        var rounds = new List<GameRound> { match.GameRounds!.First() };

        var matchEndResult = new MatchEndResult(
            matchId,
            Guid.Empty,
            Guid.Empty,
            15000,
            15000,
            0,
            0,
            rounds
        );

        var response = MatchEndedResponse.FromMatchEndResult(matchEndResult);

        Assert.Null(response.WinnerId);
        Assert.Null(response.LoserId);
    }

    [Fact]
    public void FromMatchEndResult_WithNonEmptyWinnerId_ShouldReturnWinner()
    {
        var matchId = Guid.NewGuid();
        var winnerId = Guid.NewGuid();
        var loserId = Guid.NewGuid();

        var match = GameMatch.StartGameMatch(Guid.NewGuid(), Guid.NewGuid());
        var correctLocation = new Coordinate(-23.5505, -46.6333);
        var playerAGuess = new Coordinate(-23.5500, -46.6330);
        var playerBGuess = new Coordinate(-23.5510, -46.6340);

        match.StartNewGameRound();
        match.EndCurrentGameRound(correctLocation, playerAGuess, playerBGuess);

        var rounds = new List<GameRound> { match.GameRounds!.First() };

        var matchEndResult = new MatchEndResult(
            matchId,
            winnerId,
            loserId,
            18000,
            12000,
            30,
            5,
            rounds
        );

        var response = MatchEndedResponse.FromMatchEndResult(matchEndResult);

        Assert.Equal(winnerId, response.WinnerId);
        Assert.Equal(loserId, response.LoserId);
        Assert.Equal(30, response.PointsEarned);
        Assert.Equal(5, response.PointsLost);
    }
}
