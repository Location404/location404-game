namespace Location404.Game.Infrastructure.Cache;

using Location404.Game.Application.Common.Interfaces;
using Location404.Game.Application.Features.GameRounds.Interfaces;
using Location404.Game.Application.Features.Matchmaking.Interfaces;
using Location404.Game.Domain.Entities;
using StackExchange.Redis;
using System.Globalization;

public class GuessStorageManager : IGuessStorageManager
{
    private readonly IDatabase _db;

    public GuessStorageManager(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public async Task StoreGuessAsync(Guid matchId, Guid roundId, Guid playerId, Coordinate guess)
    {
        var key = $"guess:{matchId}:{roundId}:{playerId}";
        // Use InvariantCulture to ensure decimal point (.) instead of comma (,)
        var value = $"{guess.X.ToString(CultureInfo.InvariantCulture)},{guess.Y.ToString(CultureInfo.InvariantCulture)}";
        await _db.StringSetAsync(key, value, TimeSpan.FromMinutes(5));
    }

    public async Task<(Coordinate? playerA, Coordinate? playerB)> GetBothGuessesAsync(
        Guid matchId, Guid roundId, Guid playerAId, Guid playerBId)
    {
        var guessA = await _db.StringGetAsync($"guess:{matchId}:{roundId}:{playerAId}");
        var guessB = await _db.StringGetAsync($"guess:{matchId}:{roundId}:{playerBId}");

        Coordinate? coordA = null;
        Coordinate? coordB = null;

        if (guessA.HasValue)
        {
            var parts = guessA.ToString().Split(',');
            // Use InvariantCulture for parsing to match serialization
            coordA = new Coordinate(
                double.Parse(parts[0], CultureInfo.InvariantCulture),
                double.Parse(parts[1], CultureInfo.InvariantCulture)
            );
        }

        if (guessB.HasValue)
        {
            var parts = guessB.ToString().Split(',');
            // Use InvariantCulture for parsing to match serialization
            coordB = new Coordinate(
                double.Parse(parts[0], CultureInfo.InvariantCulture),
                double.Parse(parts[1], CultureInfo.InvariantCulture)
            );
        }

        return (coordA, coordB);
    }

    public async Task ClearGuessesAsync(Guid matchId, Guid roundId)
    {
        var pattern = $"guess:{matchId}:{roundId}:*";

        var server = _db.Multiplexer.GetServer(_db.Multiplexer.GetEndPoints().First());
        var keys = server.Keys(pattern: pattern).ToArray();

        if (keys.Length > 0)
        {
            foreach (var key in keys)
            {
                await _db.KeyDeleteAsync(key);
            }
        }

        // Also remove correct answer
        var answerKey = $"answer:{matchId}:{roundId}";
        await _db.KeyDeleteAsync(answerKey);
    }

    public async Task StoreCorrectAnswerAsync(Guid matchId, Guid roundId, Coordinate correctAnswer)
    {
        var key = $"answer:{matchId}:{roundId}";
        // Use InvariantCulture to ensure decimal point (.) instead of comma (,)
        var value = $"{correctAnswer.X.ToString(CultureInfo.InvariantCulture)},{correctAnswer.Y.ToString(CultureInfo.InvariantCulture)}";
        await _db.StringSetAsync(key, value, TimeSpan.FromMinutes(5));
    }

    public async Task<Coordinate?> GetCorrectAnswerAsync(Guid matchId, Guid roundId)
    {
        var key = $"answer:{matchId}:{roundId}";
        var answer = await _db.StringGetAsync(key);

        if (!answer.HasValue)
            return null;

        var parts = answer.ToString().Split(',');
        // Use InvariantCulture for parsing to match serialization
        return new Coordinate(
            double.Parse(parts[0], CultureInfo.InvariantCulture),
            double.Parse(parts[1], CultureInfo.InvariantCulture)
        );
    }
}