namespace Location404.Game.Infrastructure.Configuration;

public class Location404DataSettings
{
    public string BaseUrl { get; set; } = "http://localhost:5000";
    public int TimeoutSeconds { get; set; } = 10;
}
