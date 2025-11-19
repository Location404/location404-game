namespace Location404.Game.Application.Common.Interfaces;

public interface IPlayerConnectionManager
{
    Task MapPlayerToConnectionAsync(Guid playerId, string connectionId);
    Task<string?> GetConnectionIdAsync(Guid playerId);
    Task RemoveMappingAsync(Guid playerId);
}