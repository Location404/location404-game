using Location404.Game.Application.DTOs.Integration;

namespace Location404.Game.Application.Common.Interfaces;

public interface IGameDataClient
{
    Task<UserDto?> GetUserAsync(Guid userId);
    Task<bool> IsUserAvailableAsync(Guid userId);
    Task UpdateUserStatsAsync(UpdateUserStatsRequest request);
}