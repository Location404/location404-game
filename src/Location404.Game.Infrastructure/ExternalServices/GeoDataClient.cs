using Location404.Game.Application.DTOs;
using Location404.Game.Application.Events;
using Location404.Game.Application.Common.Interfaces;
using Location404.Game.Application.Features.GameRounds.Interfaces;
using Location404.Game.Application.Features.Matchmaking.Interfaces;
using Location404.Game.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace Location404.Game.Infrastructure.ExternalServices;

public class GeoDataClient : IGeoDataClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeoDataClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public GeoDataClient(HttpClient httpClient, ILogger<GeoDataClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<LocationDto?> GetRandomLocationAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching random location from geo-data-service");

            var response = await _httpClient.GetAsync("/api/locations/random", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch random location. Status: {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var location = JsonSerializer.Deserialize<LocationDto>(content, _jsonOptions);

            if (location != null)
            {
                _logger.LogInformation("Fetched location: {Name} (Lat: {Lat}, Lng: {Lng})",
                    location.Name, location.Coordinate.X, location.Coordinate.Y);
            }

            return location;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching random location from geo-data-service");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching random location");
            return null;
        }
    }

    public async Task<bool> SendMatchEndedAsync(GameMatchEndedEvent matchEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Sending match ended event via HTTP to geo-data-service for match {MatchId}", matchEvent.MatchId);

            var json = JsonSerializer.Serialize(matchEvent, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/matches/ended", content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully sent match ended event via HTTP for match {MatchId}", matchEvent.MatchId);
                return true;
            }
            else
            {
                _logger.LogWarning("Failed to send match ended event via HTTP. Status: {StatusCode}", response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending match ended event via HTTP for match {MatchId}", matchEvent.MatchId);
            return false;
        }
    }
}
