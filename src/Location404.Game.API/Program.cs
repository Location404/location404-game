using Location404.Game.API.Hubs;
using Location404.Game.API.BackgroundServices;
using Location404.Game.Infrastructure.Extensions;
using Shared.Observability.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(builder.Configuration);

var redisSettings = builder.Configuration.GetSection("Redis").Get<Location404.Game.Infrastructure.Configuration.RedisSettings>();
if (redisSettings?.Enabled == true)
{
    builder.Services.AddHostedService<RoundTimerExpirationListener>();
}

var redisConnection = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
builder.Services.AddSignalR()
    .AddStackExchangeRedis(redisConnection, options =>
    {
        options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("SignalR");
    });

builder.Services.AddOpenTelemetryObservability(builder.Configuration, options =>
{
    options.Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
});

builder.Services.AddObservabilityHealthChecks(builder.Configuration, checks =>
{
    var redisConnectionString = builder.Configuration["Redis:ConnectionString"];
    if (!string.IsNullOrEmpty(redisConnectionString))
    {
        checks.AddRedis(redisConnectionString, name: "redis", tags: new[] { "ready", "db" }, timeout: TimeSpan.FromSeconds(3));
    }

    var rabbitMqSettings = builder.Configuration.GetSection("RabbitMQ");
    var rabbitHost = rabbitMqSettings["HostName"];
    var rabbitPort = rabbitMqSettings.GetValue<int>("Port");
    var rabbitUser = rabbitMqSettings["UserName"];
    var rabbitPass = rabbitMqSettings["Password"];
    var rabbitVHost = rabbitMqSettings["VirtualHost"];

    if (!string.IsNullOrEmpty(rabbitHost))
    {
        var connectionString = $"amqp://{rabbitUser}:{rabbitPass}@{rabbitHost}:{rabbitPort}/{rabbitVHost}";
        checks.AddRabbitMQ(sp =>
        {
            var factory = new RabbitMQ.Client.ConnectionFactory();
            factory.Uri = new Uri(connectionString);
            return factory.CreateConnectionAsync().GetAwaiter().GetResult();
        }, name: "rabbitmq", tags: new[] { "ready", "messaging" }, timeout: TimeSpan.FromSeconds(3));
    }
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? ["http://localhost:4200"]
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var signingKey = jwtSettings["SigningKey"]
    ?? throw new InvalidOperationException("JWT SigningKey is required");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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

builder.Services.AddAuthorization();

var app = builder.Build();

// CORS must be configured before endpoints
app.UseCors("AllowFrontend");

// Only use HTTPS redirection in production
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
