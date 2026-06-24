using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using NotificationProcessor.Application.Interfaces;
using NotificationProcessor.Application.Models;

namespace NotificationProcessor.Infrastructure;

public class ServiceBusNotificationPublisher : INotificationPublisher, IAsyncDisposable
{
    private readonly ServiceBusSender _sender;
    private readonly ILogger<ServiceBusNotificationPublisher> _logger;

    public ServiceBusNotificationPublisher(ServiceBusClient client, string queueName, ILogger<ServiceBusNotificationPublisher> logger)
    {
        _sender = client.CreateSender(queueName);
        _logger = logger;
    }

    public async Task PublishAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        var jsonPayload = JsonSerializer.Serialize(message);

        var serviceBusMessage = new ServiceBusMessage(jsonPayload)
        {
            MessageId = message.MessageId,
            CorrelationId = message.CorrelationId,
            ContentType = "application/json",
            Subject = message.Notification.Type
        };

        await _sender.SendMessageAsync(serviceBusMessage, cancellationToken);

        _logger.LogInformation(
            "Message published. MessageId ={MessageId}, CorrelationId={CorrelationId}, Type={Type}",
            message.MessageId,
            message.CorrelationId,
            message.Notification.Type
        );
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
    }
}