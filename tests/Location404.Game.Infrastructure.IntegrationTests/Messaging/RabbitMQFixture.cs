using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using Xunit;

namespace Location404.Game.Infrastructure.IntegrationTests.Messaging;

public class RabbitMQFixture : IAsyncLifetime
{
    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder()
        .WithImage("rabbitmq:3-management-alpine")
        .Build();

    public string ConnectionString { get; private set; } = null!;
    public string HostName { get; private set; } = null!;
    public int Port { get; private set; }
    public string UserName { get; private set; } = null!;
    public string Password { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _rabbitMqContainer.StartAsync();

        ConnectionString = _rabbitMqContainer.GetConnectionString();

        // Parse connection string to get individual components
        var factory = new ConnectionFactory();
        factory.Uri = new Uri(ConnectionString);

        HostName = factory.HostName;
        Port = factory.Port;
        UserName = factory.UserName;
        Password = factory.Password;
    }

    public async Task DisposeAsync()
    {
        await _rabbitMqContainer.DisposeAsync();
    }
}
