namespace Location404.Game.Infrastructure.Cache;

using Location404.Game.Application.Features.GameRounds.Interfaces;
using Location404.Game.Domain.Entities;
using System.Collections.Concurrent;

public class InMemoryGuessStorageManager : IGuessStorageManager
{
    private readonly ConcurrentDictionary<string, Coordinate> _guesses = new();

    private readonly ConcurrentDictionary<string, Coordinate> _correctAnswers = new();

    public Task StoreGuessAsync(Guid matchId, Guid roundId, Guid playerId, Coordinate guess)
    {
        var key = GetKey(matchId, roundId, playerId);
        _guesses[key] = guess;
        return Task.CompletedTask;
    }

    public Task<(Coordinate? playerA, Coordinate? playerB)> GetBothGuessesAsync(
        Guid matchId, Guid roundId, Guid playerAId, Guid playerBId)
    {
        var keyA = GetKey(matchId, roundId, playerAId);
        var keyB = GetKey(matchId, roundId, playerBId);

        _guesses.TryGetValue(keyA, out var guessA);
        _guesses.TryGetValue(keyB, out var guessB);

        return Task.FromResult<(Coordinate?, Coordinate?)>((guessA, guessB));
    }

    public Task ClearGuessesAsync(Guid matchId, Guid roundId)
    {
        var prefix = $"{matchId}:{roundId}:";
        var keysToRemove = _guesses.Keys
            .Where(key => key.StartsWith(prefix))
            .ToList();

        foreach (var key in keysToRemove)
        {
            _guesses.TryRemove(key, out _);
        }

        var answerKey = GetAnswerKey(matchId, roundId);
        _correctAnswers.TryRemove(answerKey, out _);

        return Task.CompletedTask;
    }

    public Task StoreCorrectAnswerAsync(Guid matchId, Guid roundId, Coordinate correctAnswer)
    {
        var key = GetAnswerKey(matchId, roundId);
        _correctAnswers[key] = correctAnswer;
        return Task.CompletedTask;
    }

    public Task<Coordinate?> GetCorrectAnswerAsync(Guid matchId, Guid roundId)
    {
        var key = GetAnswerKey(matchId, roundId);
        _correctAnswers.TryGetValue(key, out var answer);
        return Task.FromResult(answer);
    }

    private static string GetKey(Guid matchId, Guid roundId, Guid playerId)
        => $"{matchId}:{roundId}:{playerId}";

    private static string GetAnswerKey(Guid matchId, Guid roundId)
        => $"answer:{matchId}:{roundId}";
}
