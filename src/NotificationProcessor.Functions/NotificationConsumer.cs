using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using NotificationProcessor.Application.Interfaces;
using NotificationProcessor.Application.Models;
using NotificationProcessor.Infrastructure.Messaging;

namespace NotificationProcessor.Functions;

public class NotificationConsumer
{
    private readonly INotificationRepository _repository;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<NotificationConsumer> _logger;

    public NotificationConsumer(
        INotificationRepository repository,
        IEmailSender emailSender,
        ILogger<NotificationConsumer> logger)
    {
        _repository = repository;
        _emailSender = emailSender;
        _logger = logger;
    }

    [Function("NotificationConsumer")]
    public async Task Run(
        [ServiceBusTrigger("notifications", Connection = "ServiceBusConnectionString")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken cancellationToken)
    {
        var notificationMessage = JsonSerializer.Deserialize<NotificationMessage>(message.Body.ToString());

        if (notificationMessage is null)
        {
            _logger.LogError(
                "Falha ao desserializar mensagem. ServiceBusMessageId={ServiceBusMessageId}",
                message.MessageId);

            await messageActions.DeadLetterMessageAsync(
                message,
                deadLetterReason: "DeserializationFailed",
                deadLetterErrorDescription: "Não foi possível desserializar o body da mensagem.",
                cancellationToken: cancellationToken);
            return;
        }

        _logger.LogInformation(
            "Mensagem recebida. MessageId={MessageId}, CorrelationId={CorrelationId}, Type={Type}",
            notificationMessage.MessageId,
            notificationMessage.CorrelationId,
            notificationMessage.Notification.Type);

        var alreadyExists = await _repository.ExistsAsync(notificationMessage.MessageId, cancellationToken);
        if (alreadyExists)
        {
            _logger.LogWarning(
                "Mensagem duplicada detectada, ignorando. MessageId={MessageId}, CorrelationId={CorrelationId}",
                notificationMessage.MessageId,
                notificationMessage.CorrelationId);

            await messageActions.CompleteMessageAsync(message, cancellationToken);
            return;
        }

        try
        {
            var record = new NotificationRecord
            {
                MessageId = notificationMessage.MessageId,
                CorrelationId = notificationMessage.CorrelationId,
                Type = notificationMessage.Notification.Type,
                Recipient = notificationMessage.Notification.Recipient,
                Subject = notificationMessage.Notification.Subject,
                Body = notificationMessage.Notification.Body,
                Status = "Pending",
                CreatedAt = notificationMessage.CreatedAt
            };

            await _repository.InsertAsync(record, cancellationToken);

            await SendNotificationAsync(notificationMessage.Notification, cancellationToken);

            await _repository.UpdateStatusAsync(notificationMessage.MessageId, "Processed", cancellationToken);

            _logger.LogInformation(
                "Notificação processada com sucesso. MessageId={MessageId}, CorrelationId={CorrelationId}",
                notificationMessage.MessageId,
                notificationMessage.CorrelationId);

            await messageActions.CompleteMessageAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Erro ao processar notificação. MessageId={MessageId}, CorrelationId={CorrelationId}, DeliveryCount={DeliveryCount}",
                notificationMessage.MessageId,
                notificationMessage.CorrelationId,
                message.DeliveryCount);

            await messageActions.AbandonMessageAsync(message, cancellationToken: cancellationToken);
            throw;
        }
    }

    private async Task SendNotificationAsync(NotificationRequest notification, CancellationToken cancellationToken)
    {
        switch (notification.Type.ToLower())
        {
            case "email":
                await _emailSender.SendAsync(
                    notification.Recipient,
                    notification.Subject,
                    notification.Body,
                    cancellationToken);
                break;

            case "sms":
                // Twilio — implementar no próximo passo
                _logger.LogInformation(
                    "SMS notification queued. Recipient={Recipient}",
                    notification.Recipient);
                break;

            case "push":
                // Firebase — implementar futuramente
                _logger.LogInformation(
                    "Push notification queued. Recipient={Recipient}",
                    notification.Recipient);
                break;

            default:
                _logger.LogWarning(
                    "Unknown notification type: {Type}",
                    notification.Type);
                break;
        }
    }
}