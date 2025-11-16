namespace Location404.Game.Domain.Entities;

public class GameMatch
{
    public Guid Id { get; private set; }
    public DateTime StartTime { get; private set; }
    public DateTime EndTime { get; private set; }

    public Guid PlayerAId { get; private set; }
    public Guid PlayerBId { get; private set; }

    public Guid? PlayerWinnerId { get; private set; }
    public Guid? PlayerLoserId { get; private set; }

    public int? PlayerATotalPoints { get; private set; }
    public int? PlayerBTotalPoints { get; private set; }

    public int? PointsEarned { get; private set; }
    public int? PointsLost { get; private set; }

    public List<GameRound>? GameRounds { get; private set; }
    public GameRound? CurrentGameRound { get; private set; }
    public int TotalRounds => GameRounds?.Count ?? 0;
    private static readonly int MaxRounds = 3;

    private GameMatch() { }

    public GameMatch(Guid id, Guid playerAId, Guid playerBId, DateTime startTime, DateTime endTime, Guid playerWinnerId, Guid playerLoserId, int pointsEarned, int pointsLost)
    {
        Id = id;
        PlayerAId = playerAId;
        PlayerBId = playerBId;
        StartTime = startTime;
        EndTime = endTime;
        PlayerWinnerId = playerWinnerId;
        PlayerLoserId = playerLoserId;
        PointsEarned = pointsEarned;
        PointsLost = pointsLost;
    }

    public static GameMatch StartGameMatch(Guid playerAId, Guid playerBId)
    {
        var gameMatch = new GameMatch()
        {
            Id = Guid.NewGuid(),
            PlayerAId = playerAId,
            PlayerBId = playerBId,
            StartTime = DateTime.UtcNow,

            PlayerATotalPoints = 0,
            PlayerBTotalPoints = 0,
        };

        return gameMatch;
    }

    public void EndGameMatch()
    {
        if (PlayerATotalPoints > PlayerBTotalPoints)
        {
            PlayerWinnerId = PlayerAId;
            PlayerLoserId = PlayerBId;
            PointsEarned = CalculatePointsEarned(PlayerATotalPoints, PlayerBTotalPoints);
            PointsLost = CalculatePointsLost(PlayerATotalPoints, PlayerBTotalPoints);
        }
        else if (PlayerBTotalPoints > PlayerATotalPoints)
        {
            PlayerWinnerId = PlayerBId;
            PlayerLoserId = PlayerAId;
            PointsEarned = CalculatePointsEarned(PlayerBTotalPoints, PlayerATotalPoints);
            PointsLost = CalculatePointsLost(PlayerBTotalPoints, PlayerATotalPoints);
        }

        EndTime = DateTime.UtcNow;
    }
    

    private int CalculatePointsEarned(int? winnerPoints, int? loserPoints)
    {
        if (winnerPoints == null || loserPoints == null)
            throw new InvalidOperationException("Points are not calculated.");

        var pointDifference = winnerPoints.Value - loserPoints.Value;

        if (pointDifference >= 20)
            return 100;
        else if (pointDifference >= 10)
            return 75;
        else if (pointDifference >= 0)
            return 50;
        else
            return 30;
    }
    private int CalculatePointsLost(int? winnerPoints, int? loserPoints)
    {
        if (winnerPoints == null || loserPoints == null)
            throw new InvalidOperationException("Points are not calculated.");

        var pointDifference = winnerPoints.Value - loserPoints.Value;

        if (pointDifference >= 20)
            return 30;
        else if (pointDifference >= 10)
            return 50;
        else if (pointDifference >= 0)
            return 75;
        else
            return 100;
    }

    public void StartNewGameRound()
    {
        if (CurrentGameRound != null && !CurrentGameRound.GameRoundEnded)
            throw new InvalidOperationException("Current game round is not ended.");

        CurrentGameRound = GameRound.StartGameRound(Id, TotalRounds + 1, PlayerAId, PlayerBId);
    }

    public void EndCurrentGameRound(Coordinate gameResponse, Coordinate? playerAGuess, Coordinate? playerBGuess)
    {
        if (CurrentGameRound == null)
            throw new InvalidOperationException("No current game round to end.");

        CurrentGameRound.EndGameRound(gameResponse, playerAGuess, playerBGuess);

        PlayerATotalPoints += CurrentGameRound.PlayerAPoints;
        PlayerBTotalPoints += CurrentGameRound.PlayerBPoints;

        GameRounds ??= [];

        GameRounds.Add(CurrentGameRound);
        CurrentGameRound = null;
    }

    public bool CanStartNewRound()
    {
        return TotalRounds < MaxRounds && (CurrentGameRound == null || CurrentGameRound.GameRoundEnded);
    }
}