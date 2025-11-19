namespace Location404.Game.Infrastructure.HttpClients;

using Location404.Game.Application.Common.Interfaces;
using Location404.Game.Application.Features.GameRounds.Interfaces;
using Location404.Game.Application.Features.Matchmaking.Interfaces;
using Location404.Game.Application.DTOs.Integration;
using System.Net.Http.Json;

public class GameDataHttpClient(HttpClient httpClient) : IGameDataClient
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<UserDto?> GetUserAsync(Guid userId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/users/{userId}");

            return !response.IsSuccessStatusCode 
                ? null 
                : await response.Content.ReadFromJsonAsync<UserDto>();
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<bool> IsUserAvailableAsync(Guid userId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/users/{userId}/availability");
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    public async Task UpdateUserStatsAsync(UpdateUserStatsRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/users/stats/update",
                request
            );

            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Failed to update user stats: {ex.Message}", 
                ex
            );
        }
    }
}