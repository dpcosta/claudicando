namespace Orders.Api.Services;

/// <summary>
/// Interface para publicação de eventos no RabbitMQ
/// </summary>
public interface IRabbitMqPublisher
{
    /// <summary>
    /// Publica um evento no RabbitMQ
    /// </summary>
    /// <param name="eventType">Tipo do evento (usado como routing key)</param>
    /// <param name="payload">Payload do evento em JSON</param>
    /// <returns>Task representando a operação assíncrona</returns>
    Task PublishAsync(string eventType, string payload);
}
