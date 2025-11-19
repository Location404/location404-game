using Location404.Game.Application.Events;

namespace Location404.Game.Application.Common.Interfaces;

public interface IGameEventPublisher
{
    Task PublishMatchEndedAsync(GameMatchEndedEvent @event);
    Task PublishRoundEndedAsync(GameRoundEndedEvent @event);
}