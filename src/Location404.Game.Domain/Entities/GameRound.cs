namespace Location404.Game.Domain.Entities;

public class GameRound
{
    public Guid Id { get; private set; }
    public Guid GameMatchId { get; private set; }
    public int RoundNumber { get; private set; }

    public Guid PlayerAId { get; private set; }
    public Guid PlayerBId { get; private set; }

    public int? PlayerAPoints { get; private set; }
    public int? PlayerBPoints { get; private set; }

    public Coordinate? GameResponse { get; private set; }

    public Coordinate? PlayerAGuess { get; private set; }
    public Coordinate? PlayerBGuess { get; private set; }

    public bool GameRoundEnded { get; private set; } = false;

    private GameRound() { }

    public static GameRound StartGameRound(Guid gameMatchId, int roundNumber, Guid playerAId, Guid playerBId)
    {
        var gameRound = new GameRound()
        {
            Id = Guid.NewGuid(),
            GameMatchId = gameMatchId,
            RoundNumber = roundNumber,
            PlayerAId = playerAId,
            PlayerBId = playerBId
        };

        return gameRound;
    }

    public void EndGameRound(Coordinate gameResponse, Coordinate? playerAGuess, Coordinate? playerBGuess)
    {
        GameResponse = gameResponse;
        PlayerAGuess = playerAGuess;
        PlayerBGuess = playerBGuess;

        PlayerAPoints = CalculatePoints(playerAGuess);
        PlayerBPoints = CalculatePoints(playerBGuess);
        GameRoundEnded = true;
    }

    private int CalculatePoints(Coordinate? playerGuess)
    {
        if (GameResponse == null)
            throw new InvalidOperationException("Game response is not set.");

        if (playerGuess == null)
            return 0;

        var distanceKm = GameResponse.CalculateDistance(playerGuess);

        // GeoGuessr-style exponential decay scoring
        // Formula: score = 5000 * e^(-distance / scaleFactor)
        //
        // Scale factor controls how quickly points decay with distance:
        // - Smaller scale = steeper decay (harder scoring)
        // - Larger scale = gentler decay (easier scoring)
        //
        // With scaleFactor = 2000:
        // - 0 km      → 5000 points (perfect!)
        // - 100 km    → ~4756 points (excellent)
        // - 500 km    → ~3894 points (very good)
        // - 1000 km   → ~3033 points (good)
        // - 2000 km   → ~1839 points (decent)
        // - 5000 km   → ~410 points (far)
        // - 10000 km  → ~34 points (very far)
        // - 20000 km  → ~0 points (opposite side of Earth)

        const double maxScore = 5000.0;
        const double scaleFactor = 2000.0; // Controls decay rate

        var score = maxScore * Math.Exp(-distanceKm / scaleFactor);

        // Round to nearest integer and ensure minimum score of 0
        return Math.Max(0, (int)Math.Round(score));
    }

    public Guid? PlayerWinner()
    {
        if (PlayerAPoints == null || PlayerBPoints == null)
            throw new InvalidOperationException("Round has not ended or points are not calculated.");

        if (PlayerAPoints > PlayerBPoints)
            return PlayerAId;
        else if (PlayerBPoints > PlayerAPoints)
            return PlayerBId;
        else
            return null;
    }
}