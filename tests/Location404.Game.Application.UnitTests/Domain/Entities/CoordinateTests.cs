using Location404.Game.Domain.Entities;
using Xunit;

namespace Location404.Game.Application.UnitTests.Domain.Entities;

public class CoordinateTests
{
    [Fact]
    public void Constructor_ShouldSetCorrectXAndY()
    {
        // Arrange & Act
        var coordinate = new Coordinate(x: -23.5505, y: -46.6333); // São Paulo

        // Assert
        Assert.Equal(-23.5505, coordinate.X);
        Assert.Equal(-46.6333, coordinate.Y);
    }

    [Fact]
    public void CalculateDistance_SameCoordinate_ShouldReturnZero()
    {
        // Arrange
        var coord1 = new Coordinate(x: -23.5505, y: -46.6333);
        var coord2 = new Coordinate(x: -23.5505, y: -46.6333);

        // Act
        var distance = coord1.CalculateDistance(coord2);

        // Assert
        Assert.Equal(0, distance, precision: 2);
    }

    [Fact]
    public void CalculateDistance_KnownDistance_ShouldCalculateCorrectly()
    {
        // Arrange - São Paulo to Rio de Janeiro
        var saoPaulo = new Coordinate(x: -23.5505, y: -46.6333);
        var rioDeJaneiro = new Coordinate(x: -22.9068, y: -43.1729);

        // Act
        var distance = saoPaulo.CalculateDistance(rioDeJaneiro);

        // Assert - Expected: ~360 km
        Assert.InRange(distance, 350, 370);
    }

    [Fact]
    public void CalculateDistance_OppositeEndsOfEarth_ShouldCalculateCorrectly()
    {
        // Arrange - North Pole to South Pole (approximately)
        var northPole = new Coordinate(x: 90, y: 0);
        var southPole = new Coordinate(x: -90, y: 0);

        // Act
        var distance = northPole.CalculateDistance(southPole);

        // Assert - Expected: ~20,000 km (half Earth's circumference)
        Assert.InRange(distance, 19900, 20100);
    }

    [Fact]
    public void CalculateDistance_LondonToNewYork_ShouldCalculateCorrectly()
    {
        // Arrange
        var london = new Coordinate(x: 51.5074, y: -0.1278);
        var newYork = new Coordinate(x: 40.7128, y: -74.0060);

        // Act
        var distance = london.CalculateDistance(newYork);

        // Assert - Expected: ~5,570 km
        Assert.InRange(distance, 5500, 5650);
    }

    [Fact]
    public void CalculateDistance_TokyoToSydney_ShouldCalculateCorrectly()
    {
        // Arrange
        var tokyo = new Coordinate(x: 35.6762, y: 139.6503);
        var sydney = new Coordinate(x: -33.8688, y: 151.2093);

        // Act
        var distance = tokyo.CalculateDistance(sydney);

        // Assert - Expected: ~7,800 km
        Assert.InRange(distance, 7700, 7900);
    }

    [Fact]
    public void CalculateDistance_SmallDistance_ShouldBePrecise()
    {
        // Arrange - Two points very close together (1 km apart approximately)
        var coord1 = new Coordinate(x: -23.5505, y: -46.6333);
        var coord2 = new Coordinate(x: -23.5595, y: -46.6333); // ~1 km north

        // Act
        var distance = coord1.CalculateDistance(coord2);

        // Assert - Should be close to 1 km
        Assert.InRange(distance, 0.9, 1.1);
    }

    [Fact]
    public void CalculateDistance_CrossingDateLine_ShouldCalculateCorrectly()
    {
        // Arrange - Points on opposite sides of date line
        var coord1 = new Coordinate(x: 0, y: 179);
        var coord2 = new Coordinate(x: 0, y: -179);

        // Act
        var distance = coord1.CalculateDistance(coord2);

        // Assert - Should be ~222 km (short way around), not ~39,778 km (long way)
        Assert.InRange(distance, 200, 250);
    }

    [Fact]
    public void CalculateDistance_Equator_ShouldCalculateCorrectly()
    {
        // Arrange - Two points on equator, 10 degrees apart
        var coord1 = new Coordinate(x: 0, y: 0);
        var coord2 = new Coordinate(x: 0, y: 10);

        // Act
        var distance = coord1.CalculateDistance(coord2);

        // Assert - Expected: ~1,111 km (approximately 111 km per degree at equator)
        Assert.InRange(distance, 1100, 1120);
    }

    [Fact]
    public void CalculateDistance_NegativeCoordinates_ShouldWorkCorrectly()
    {
        // Arrange - Both coordinates negative
        var coord1 = new Coordinate(x: -34.6037, y: -58.3816); // Buenos Aires
        var coord2 = new Coordinate(x: -33.4489, y: -70.6693); // Santiago

        // Act
        var distance = coord1.CalculateDistance(coord2);

        // Assert - Expected: ~1,150 km
        Assert.InRange(distance, 1100, 1200);
    }

    [Fact]
    public void CalculateDistance_VerySmallDistance_ShouldNotReturnNegative()
    {
        // Arrange - Almost identical coordinates
        var coord1 = new Coordinate(x: 40.7128, y: -74.0060);
        var coord2 = new Coordinate(x: 40.7129, y: -74.0061);

        // Act
        var distance = coord1.CalculateDistance(coord2);

        // Assert
        Assert.True(distance >= 0);
        Assert.InRange(distance, 0, 0.2); // Less than 200 meters
    }
}
