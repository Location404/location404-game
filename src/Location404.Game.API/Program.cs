using Location404.Game.API.Hubs;
using Location404.Game.API.BackgroundServices;
using Location404.Game.Infrastructure.Extensions;
using Location404.Game.Infrastructure;
using Shared.Observability.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddGameApplicationServices();

AddBackgroundServices(builder.Services, builder.Configuration);
AddSignalRWithRedis(builder.Services, builder.Configuration);
AddObservability(builder.Services, builder.Configuration);
AddCorsPolicy(builder.Services, builder.Configuration);
AddJwtAuthentication(builder.Services, builder.Configuration);

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseCors("AllowFrontend");

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapHub<GameHub>("/gamehub");
app.MapObservabilityHealthChecks();

app.Run();

void AddBackgroundServices(IServiceCollection services, IConfiguration configuration)
{
    var redisSettings = configuration.GetSection("Redis").Get<Location404.Game.Infrastructure.Configuration.RedisSettings>();
    if (redisSettings?.Enabled == true)
    {
        services.AddHostedService<RoundTimerExpirationListener>();
    }
}

void AddSignalRWithRedis(IServiceCollection services, IConfiguration configuration)
{
    var redisSettings = configuration.GetSection("Redis").Get<Location404.Game.Infrastructure.Configuration.RedisSettings>();
    if (redisSettings?.Enabled == true)
    {
        services.AddSignalR()
            .AddStackExchangeRedis(options =>
            {
                options.ConnectionFactory = async writer =>
                {
                    return writer.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>();
                };
                options.Configuration.ChannelPrefix = "SignalR";
            });
    }
    else
    {
        services.AddSignalR();
    }
}

void AddObservability(IServiceCollection services, IConfiguration configuration)
{
    services.AddOpenTelemetryObservability(configuration, options =>
    {
        options.Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
    });

    services.AddObservabilityHealthChecks(configuration, checks =>
    {
        var redisSettings = configuration.GetSection("Redis").Get<Location404.Game.Infrastructure.Configuration.RedisSettings>();
        if (redisSettings?.Enabled == true)
        {
            checks.AddRedis(sp => sp.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>(), name: "redis", tags: new[] { "ready", "db" }, timeout: TimeSpan.FromSeconds(3));
        }

        var rabbitSettings = configuration.GetSection("RabbitMQ").Get<Location404.Game.Infrastructure.Configuration.RabbitMQSettings>();
        if (rabbitSettings?.Enabled == true)
        {
            checks.AddRabbitMQ(sp =>
            {
                var factory = sp.GetRequiredService<RabbitMQ.Client.IConnectionFactory>();
                return factory.CreateConnectionAsync();
            }, name: "rabbitmq", tags: new[] { "ready", "messaging" }, timeout: TimeSpan.FromSeconds(3));
        }
    });
}

void AddCorsPolicy(IServiceCollection services, IConfiguration configuration)
{
    services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", policy =>
        {
            policy.WithOrigins(
                    configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                    ?? ["http://localhost:4200"]
                )
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });
}

void AddJwtAuthentication(IServiceCollection services, IConfiguration configuration)
{
    var jwtSettings = configuration.GetSection("JwtSettings");
    var signingKey = jwtSettings["SigningKey"]
        ?? throw new InvalidOperationException("JWT SigningKey is required");

    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidAudience = jwtSettings["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                ClockSkew = TimeSpan.Zero
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;

                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/gamehub"))
                    {
                        context.Token = accessToken;
                    }
                    else
                    {
                        context.Token = context.Request.Cookies["accessToken"];
                    }

                    return Task.CompletedTask;
                }
            };
        });
}
