namespace Location404.Game.Infrastructure.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Location404.Game.Infrastructure.Configuration;
using Location404.Game.Application.Services;
using Location404.Game.Infrastructure.Cache;
using Location404.Game.Infrastructure.Matchmaking;
using Location404.Game.Infrastructure.Messaging;
using Location404.Game.Infrastructure.ExternalServices;
using Location404.Game.Infrastructure.HttpClients;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRedisServices(configuration);
        services.AddMatchmakingServices(configuration);
        services.AddMessagingServices(configuration);
        services.AddHttpClients(configuration);
        // services.AddApplicationServices();

        return services;
    }

    private static IServiceCollection AddRedisServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RedisSettings>(configuration.GetSection("Redis"));

        var redisSettings = configuration.GetSection("Redis").Get<RedisSettings>()
            ?? new RedisSettings();

        if (redisSettings.Enabled)
        {
            // Use real Redis implementations
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<IConnectionMultiplexer>>();

                try
                {
                    var configurationOptions = ConfigurationOptions.Parse(redisSettings.ConnectionString);

                    configurationOptions.AbortOnConnectFail = false;
                    configurationOptions.ConnectTimeout = 5000;
                    configurationOptions.SyncTimeout = 5000;
                    configurationOptions.AsyncTimeout = 5000;
                    configurationOptions.ConnectRetry = 3;
                    configurationOptions.KeepAlive = 60;

                    // Dragonfly compatibility settings
                    configurationOptions.AllowAdmin = false;
                    configurationOptions.DefaultDatabase = 0;

                    logger.LogInformation("Connecting to Redis/Dragonfly at: {Endpoint}",
                        string.Join(", ", configurationOptions.EndPoints));

                    return ConnectionMultiplexer.Connect(configurationOptions);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to connect to Redis/Dragonfly. Check connection string format: {ConnectionString}",
                        redisSettings.ConnectionString);
                    throw;
                }
            });

            services.AddSingleton<IGameMatchManager, RedisGameMatchManager>();
            services.AddSingleton<IPlayerConnectionManager, PlayerConnectionManager>();
            services.AddSingleton<IGuessStorageManager, GuessStorageManager>();
            services.AddSingleton<IRoundTimerService, RedisRoundTimerService>();
        }
        else
        {
            services.AddSingleton<IGameMatchManager, InMemoryGameMatchManager>();
            services.AddSingleton<IPlayerConnectionManager, InMemoryPlayerConnectionManager>();
            services.AddSingleton<IGuessStorageManager, InMemoryGuessStorageManager>();
            services.AddSingleton<IRoundTimerService, InMemoryRoundTimerService>();
        }

        return services;
    }

    private static IServiceCollection AddMatchmakingServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var redisSettings = configuration.GetSection("Redis").Get<RedisSettings>()
            ?? new RedisSettings();

        if (redisSettings.Enabled)
        {
            services.AddSingleton<IMatchmakingService, RedisMatchmakingService>();
        }
        else
        {
            services.AddSingleton<IMatchmakingService, InMemoryMatchmakingService>();
        }

        return services;
    }

    private static IServiceCollection AddMessagingServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RabbitMQSettings>(configuration.GetSection("RabbitMQ"));

        var rabbitSettings = configuration.GetSection("RabbitMQ").Get<RabbitMQSettings>()
            ?? new RabbitMQSettings();

        // Use mock if RabbitMQ is disabled or in development without RabbitMQ
        if (rabbitSettings.Enabled)
        {
            services.AddSingleton<IGameEventPublisher, RabbitMQEventPublisher>();
        }
        else
        {
            services.AddSingleton<IGameEventPublisher, MockGameEventPublisher>();
        }

        return services;
    }

    private static IServiceCollection AddHttpClients(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GameDataClientSettings>(configuration.GetSection("GameDataClient"));
        services.Configure<GeoDataServiceSettings>(configuration.GetSection("GeoDataService"));

        var gameDataSettings = configuration.GetSection("GameDataClient").Get<GameDataClientSettings>()
            ?? new GameDataClientSettings();

        var geoDataSettings = configuration.GetSection("GeoDataService").Get<GeoDataServiceSettings>()
            ?? new GeoDataServiceSettings();

        services.AddHttpClient<IGameDataClient, GameDataHttpClient>(client =>
        {
            client.BaseAddress = new Uri(gameDataSettings.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(gameDataSettings.TimeoutSeconds);
        });

        services.AddHttpClient<IGeoDataClient, GeoDataClient>(client =>
        {
            client.BaseAddress = new Uri(geoDataSettings.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(geoDataSettings.TimeoutSeconds);
        })
        .AddPolicyHandler(GetRetryPolicy())
        .AddPolicyHandler(GetCircuitBreakerPolicy());

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
            .WaitAndRetryAsync(3, retryAttempt =>
            {
                var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
                var jitter = Random.Shared.NextDouble() * 0.4 - 0.2;
                var jitterMs = baseDelay.TotalMilliseconds * jitter;
                return baseDelay + TimeSpan.FromMilliseconds(jitterMs);
            });
    }

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
    }
}