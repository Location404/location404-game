using Location404.Game.Domain.Entities;

namespace Location404.Game.Application.Features.GameRounds.Commands.StartRoundCommand;

public record StartRoundCommandResponse(
    Guid MatchId,
    Guid RoundId,
    int RoundNumber,
    Coordinate Location,
    int? Heading,
    int? Pitch,
    DateTimeOffset StartedAt,
    int DurationSeconds
);
