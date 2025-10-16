using System.Text;
using RabbitMQ.Client;

namespace Orders.Api.Services;

/// <summary>
/// Implementação do publisher RabbitMQ com gerenciamento de conexão e reconexão automática
/// </summary>
public class RabbitMqPublisher : IRabbitMqPublisher, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private bool _disposed;

    public RabbitMqPublisher(IConfiguration configuration, ILogger<RabbitMqPublisher> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Publica um evento no RabbitMQ usando exchange do tipo topic
    /// </summary>
    public async Task PublishAsync(string eventType, string payload)
    {
        await EnsureConnectionAsync();

        if (_channel == null)
        {
            throw new InvalidOperationException("RabbitMQ channel não está disponível");
        }

        try
        {
            var exchangeName = _configuration["RabbitMQ:ExchangeName"] ?? "orders-events";
            var exchangeType = _configuration["RabbitMQ:ExchangeType"] ?? "topic";

            // Declarar exchange (idempotente)
            await _channel.ExchangeDeclareAsync(
                exchange: exchangeName,
                type: exchangeType,
                durable: true,
                autoDelete: false
            );

            // Converter payload para bytes
            var body = Encoding.UTF8.GetBytes(payload);

            // Publicar mensagem com routing key baseada no eventType
            await _channel.BasicPublishAsync(
                exchange: exchangeName,
                routingKey: eventType,
                body: body,
                mandatory: false
            );

            _logger.LogInformation("Event {EventType} published to RabbitMQ exchange {ExchangeName}",
                eventType, exchangeName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing event {EventType} to RabbitMQ", eventType);

            // Invalidar conexão para forçar reconexão na próxima tentativa
            await DisconnectAsync();
            throw;
        }
    }

    /// <summary>
    /// Garante que a conexão e o canal estejam disponíveis
    /// </summary>
    private async Task EnsureConnectionAsync()
    {
        if (_connection != null && _connection.IsOpen && _channel != null && _channel.IsOpen)
        {
            return;
        }

        await _connectionLock.WaitAsync();

        try
        {
            // Double-check após adquirir o lock
            if (_connection != null && _connection.IsOpen && _channel != null && _channel.IsOpen)
            {
                return;
            }

            await DisconnectAsync();
            await ConnectAsync();
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Estabelece conexão com o RabbitMQ
    /// </summary>
    private async Task ConnectAsync()
    {
        try
        {
            var connectionString = _configuration.GetConnectionString("rabbitmq");

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("RabbitMQ connection string não encontrada");
            }

            var factory = new ConnectionFactory
            {
                Uri = new Uri(connectionString),
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                ClientProvidedName = "Orders.Api-Publisher"
            };

            _connection = await factory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();

            _logger.LogInformation("Connected to RabbitMQ at {Host}", factory.Uri.Host);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to RabbitMQ");
            throw;
        }
    }

    /// <summary>
    /// Fecha conexão e canal
    /// </summary>
    private async Task DisconnectAsync()
    {
        if (_channel != null)
        {
            try
            {
                await _channel.CloseAsync();
                _channel.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing RabbitMQ channel");
            }
            finally
            {
                _channel = null;
            }
        }

        if (_connection != null)
        {
            try
            {
                await _connection.CloseAsync();
                _connection.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing RabbitMQ connection");
            }
            finally
            {
                _connection = null;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            DisconnectAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing RabbitMqPublisher");
        }
        finally
        {
            _connectionLock.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
