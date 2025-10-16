using Microsoft.EntityFrameworkCore;
using Orders.Api.Data;

namespace Orders.Api.Services;

/// <summary>
/// Background Service que processa mensagens da Outbox e as publica no RabbitMQ
/// Implementa o padrão Transactional Outbox com processamento assíncrono
/// </summary>
public class OutboxProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly int _intervalSeconds;
    private readonly int _batchSize;

    public OutboxProcessor(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<OutboxProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _intervalSeconds = configuration.GetValue<int>("OutboxProcessor:IntervalSeconds", 5);
        _batchSize = configuration.GetValue<int>("OutboxProcessor:BatchSize", 10);

        _logger.LogInformation(
            "OutboxProcessor initialized with IntervalSeconds={IntervalSeconds}, BatchSize={BatchSize}",
            _intervalSeconds, _batchSize);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessor started");

        // Aguardar um pouco antes de iniciar o processamento para garantir que a aplicação está pronta
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages batch");
            }

            // Aguardar intervalo configurado antes da próxima execução
            await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
        }

        _logger.LogInformation("OutboxProcessor stopped");
    }

    /// <summary>
    /// Processa um batch de mensagens da Outbox
    /// </summary>
    private async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IRabbitMqPublisher>();

        // Buscar mensagens não processadas (FIFO)
        var messages = await dbContext.OutboxMessages
            .Where(m => !m.IsProcessed)
            .OrderBy(m => m.CreatedAt)
            .Take(_batchSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Processing {Count} outbox messages", messages.Count);

        var processedCount = 0;
        var errorCount = 0;

        foreach (var message in messages)
        {
            try
            {
                // Publicar no RabbitMQ
                await publisher.PublishAsync(message.EventType, message.Payload);

                // Marcar como processada
                message.IsProcessed = true;
                message.ProcessedAt = DateTime.UtcNow;

                processedCount++;

                _logger.LogInformation(
                    "Outbox message {MessageId} of type {EventType} published successfully",
                    message.Id, message.EventType);
            }
            catch (Exception ex)
            {
                errorCount++;

                _logger.LogError(ex,
                    "Error processing outbox message {MessageId} of type {EventType}. Will retry in next batch.",
                    message.Id, message.EventType);

                // Não marcar como processada - será tentada novamente na próxima execução
                // Isso implementa retry automático
            }
        }

        // Salvar mudanças (apenas mensagens processadas com sucesso)
        if (processedCount > 0)
        {
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Outbox batch completed: {ProcessedCount} processed, {ErrorCount} errors",
                    processedCount, errorCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving outbox messages state to database");
                throw;
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("OutboxProcessor is stopping, processing remaining messages...");

        // Tentar processar mensagens pendentes antes de parar
        try
        {
            await ProcessOutboxMessagesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error processing final batch during shutdown");
        }

        await base.StopAsync(cancellationToken);
    }
}
