namespace Location404.Game.Domain.Entities;

public class Coordinate(double x, double y)
{
    public double X { get; private set; } = x;
    public double Y { get; private set; } = y;

    /// <summary>
    /// Calculate distance using Haversine formula for geographic coordinates
    /// X = Latitude, Y = Longitude
    /// Returns distance in kilometers
    /// </summary>
    public double CalculateDistance(Coordinate other)
    {
        const double earthRadiusKm = 6371.0;

        var lat1 = DegreesToRadians(X);
        var lat2 = DegreesToRadians(other.X);
        var deltaLat = DegreesToRadians(other.X - X);
        var deltaLon = DegreesToRadians(other.Y - Y);

        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
            Math.Cos(lat1) * Math.Cos(lat2) * 
            Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return earthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    public override bool Equals(object? obj) => obj is Coordinate other && Math.Abs(X - other.X) < 0.0001 && Math.Abs(Y - other.Y) < 0.0001;

    public override int GetHashCode() => HashCode.Combine(X, Y);

    public override string ToString() => $"({X:F4}, {Y:F4})";
}