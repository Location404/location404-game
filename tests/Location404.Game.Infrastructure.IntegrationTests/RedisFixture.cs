using StackExchange.Redis;
using Testcontainers.Redis;

namespace Location404.Game.Infrastructure.IntegrationTests;

public class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer _redisContainer = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public IConnectionMultiplexer Redis { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _redisContainer.StartAsync();
        Redis = await ConnectionMultiplexer.ConnectAsync(_redisContainer.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await Redis.DisposeAsync();
        await _redisContainer.DisposeAsync();
    }
}
