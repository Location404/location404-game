namespace Location404.Game.Infrastructure.Messaging;

using Location404.Game.Application.Events;
using Location404.Game.Application.Common.Interfaces;
using Location404.Game.Application.Features.GameRounds.Interfaces;
using Location404.Game.Application.Features.Matchmaking.Interfaces;
using Location404.Game.Infrastructure.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System.Text;
using System.Text.Json;

public class RabbitMQEventPublisher : IGameEventPublisher, IDisposable
{
    private readonly RabbitMQSettings _settings;
    private readonly IConnectionFactory _connectionFactory;
    private readonly ILogger<RabbitMQEventPublisher> _logger;
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _isDisposed;

    public RabbitMQEventPublisher(
        IOptions<RabbitMQSettings> options,
        IConnectionFactory connectionFactory,
        ILogger<RabbitMQEventPublisher> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _settings = options.Value;
        _connectionFactory = connectionFactory;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        _logger.LogInformation("RabbitMQEventPublisher initialized. Connection will be established on first event publish.");
    }

    private async Task EnsureConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_connection?.IsOpen == true && _channel?.IsOpen == true)
            return;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_connection?.IsOpen == true && _channel?.IsOpen == true)
                return;

            _logger.LogInformation("Connecting to RabbitMQ at {HostName}:{Port}", _settings.HostName, _settings.Port);

            _connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

            await _channel.ExchangeDeclareAsync(
                exchange: _settings.ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken
            );

            _logger.LogInformation("Successfully connected to RabbitMQ");
        }
        catch (BrokerUnreachableException ex)
        {
            _logger.LogError(ex, "RabbitMQ broker is unreachable at {HostName}:{Port}. Check if RabbitMQ is running and accessible.",
                _settings.HostName, _settings.Port);
            throw new InvalidOperationException($"Cannot connect to RabbitMQ at {_settings.HostName}:{_settings.Port}. Ensure RabbitMQ is running.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to establish RabbitMQ connection");
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task PublishMatchEndedAsync(GameMatchEndedEvent @event)
    {
        return PublishEventAsync("match.ended", @event);
    }

    public Task PublishRoundEndedAsync(GameRoundEndedEvent @event)
    {
        return PublishEventAsync("round.ended", @event);
    }

    private async Task PublishEventAsync<T>(string routingKey, T @event)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        const int maxRetries = 3;
        var retryCount = 0;

        while (retryCount < maxRetries)
        {
            try
            {
                await EnsureConnectionAsync();

                if (_channel == null || !_channel.IsOpen)
                {
                    throw new InvalidOperationException("RabbitMQ channel is not available");
                }

                var json = JsonSerializer.Serialize(@event, _jsonOptions);
                var body = Encoding.UTF8.GetBytes(json);

                var properties = new BasicProperties
                {
                    Persistent = true,
                    ContentType = "application/json",
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                    MessageId = Guid.NewGuid().ToString()
                };

                await _channel.BasicPublishAsync(
                    exchange: _settings.ExchangeName,
                    routingKey: routingKey,
                    mandatory: false,
                    basicProperties: properties,
                    body: body
                );

                _logger.LogInformation("Event published to RabbitMQ: {RoutingKey}, MessageId: {MessageId}",
                    routingKey, properties.MessageId);

                return;
            }
            catch (AlreadyClosedException ex)
            {
                retryCount++;
                _logger.LogWarning(ex, "RabbitMQ connection closed. Retry {RetryCount}/{MaxRetries}", retryCount, maxRetries);

                if (retryCount >= maxRetries)
                {
                    _logger.LogError(ex, "Failed to publish event after {MaxRetries} retries", maxRetries);
                    throw new InvalidOperationException($"Failed to publish event to RabbitMQ after {maxRetries} retries", ex);
                }

                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)));
                await EnsureConnectionAsync();
            }
            catch (BrokerUnreachableException ex)
            {
                retryCount++;
                _logger.LogWarning(ex, "RabbitMQ broker is unreachable. Retry {RetryCount}/{MaxRetries}", retryCount, maxRetries);

                if (retryCount >= maxRetries)
                {
                    _logger.LogError(ex, "Failed to publish event after {MaxRetries} retries - broker unreachable", maxRetries);
                    throw new InvalidOperationException($"Cannot publish event - RabbitMQ is unreachable at {_settings.HostName}:{_settings.Port}", ex);
                }

                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)));
                await EnsureConnectionAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error publishing event to RabbitMQ: {RoutingKey}", routingKey);
                throw new InvalidOperationException($"Failed to publish event to RabbitMQ: {ex.Message}", ex);
            }
        }

        throw new InvalidOperationException($"Failed to publish event after {maxRetries} retries");
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _lock.Wait();
        try
        {
            if (_isDisposed)
                return;

            _logger.LogInformation("Disposing RabbitMQ connection");

            _channel?.Dispose();
            _connection?.Dispose();

            _logger.LogInformation("RabbitMQ connection disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing RabbitMQ connection");
        }
        finally
        {
            _isDisposed = true;
            _lock.Release();
            _lock.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
