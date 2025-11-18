using LiteBus.Commands.Abstractions;
using Location404.Game.Application.Common.Result;
using Location404.Game.Domain.Entities;

namespace Location404.Game.Application.Features.GameRounds.Commands;

public class StartRoundCommand(Guid MatchId) : ICommand<Result<StartRoundResponse>>
{
    public Guid MatchId { get; } = MatchId;
}

public record StartRoundResponse(
    Guid MatchId,
    Guid RoundId,
    int RoundNumber,
    Coordinate Location,
    int? Heading,
    int? Pitch,
    DateTimeOffset StartedAt,
    int DurationSeconds
);
