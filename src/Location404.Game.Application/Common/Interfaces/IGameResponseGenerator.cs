using Location404.Game.Domain.Entities;

namespace Location404.Game.Application.Common.Interfaces;

public interface IGameResponseGenerator
{
    Task<Coordinate> GenerateRandomCoordinateAsync();
}