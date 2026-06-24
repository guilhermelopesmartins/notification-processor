using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NotificationProcessor.Application.Interfaces;
using NotificationProcessor.Application.Models;
using NotificationProcessor.Functions;
using NSubstitute;

namespace NotificationProcessor.Tests;

public class NotificationConsumerTests
{
    private readonly INotificationRepository _repository;
    private readonly ILogger<NotificationConsumer> _logger;
    private readonly NotificationConsumer _consumer;

    public NotificationConsumerTests()
    {
        _repository = Substitute.For<INotificationRepository>();
        _logger = Substitute.For<ILogger<NotificationConsumer>>();
        _consumer = new NotificationConsumer(_repository, _logger);
    }

    [Fact]
    public async Task Run_NewMessage_ShouldInsertAndMarkAsProcessed()
    {
        // Arrange
        var notificationMessage = new NotificationMessage
        {
            MessageId = Guid.NewGuid().ToString(),
            CorrelationId = Guid.NewGuid().ToString(),
            Notification = new NotificationRequest
            {
                Type = "email",
                Recipient = "teste@exemplo.com",
                Subject = "Teste",
                Body = "Corpo"
            },
            CreatedAt = DateTime.UtcNow
        };

        var serviceBusMessage = CreateServiceBusMessage(notificationMessage);

        // Mensagem nova — ainda não existe no banco
        _repository.ExistsAsync(notificationMessage.MessageId, Arg.Any<CancellationToken>())
            .Returns(false);

        var messageActions = CreateMessageActionsMock();

        // Act
        await _consumer.Run(serviceBusMessage, messageActions, CancellationToken.None);

        // Assert — deve ter inserido e atualizado o status, exatamente uma vez cada
        await _repository.Received(1).InsertAsync(
            Arg.Is<NotificationRecord>(r => r.MessageId == notificationMessage.MessageId),
            Arg.Any<CancellationToken>());

        await _repository.Received(1).UpdateStatusAsync(
            notificationMessage.MessageId, "Processed", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_DuplicateMessage_ShouldNotInsertAgain()
    {
        // Arrange — simula uma mensagem que já foi processada antes (idempotência)
        var notificationMessage = new NotificationMessage
        {
            MessageId = Guid.NewGuid().ToString(),
            CorrelationId = Guid.NewGuid().ToString(),
            Notification = new NotificationRequest
            {
                Type = "email",
                Recipient = "teste@exemplo.com",
                Subject = "Teste",
                Body = "Corpo"
            },
            CreatedAt = DateTime.UtcNow
        };

        var serviceBusMessage = CreateServiceBusMessage(notificationMessage);

        // Mensagem já existe no banco — Service Bus está reentregando
        _repository.ExistsAsync(notificationMessage.MessageId, Arg.Any<CancellationToken>())
            .Returns(true);

        var messageActions = CreateMessageActionsMock();

        // Act
        await _consumer.Run(serviceBusMessage, messageActions, CancellationToken.None);

        // Assert — NÃO deve inserir nem atualizar status novamente
        await _repository.DidNotReceive().InsertAsync(
            Arg.Any<NotificationRecord>(), Arg.Any<CancellationToken>());

        await _repository.DidNotReceive().UpdateStatusAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------
    // Helpers para criar os mocks necessários para testar a Function
    // -----------------------------------------------------------------

    private static ServiceBusReceivedMessage CreateServiceBusMessage(NotificationMessage notification)
    {
        var json = JsonSerializer.Serialize(notification);
        var body = BinaryData.FromString(json);

        return ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: body,
            messageId: notification.MessageId,
            correlationId: notification.CorrelationId);
    }

    private static ServiceBusMessageActions CreateMessageActionsMock()
    {
        // ServiceBusMessageActions não tem construtor público acessível para mock direto,
        // então usamos NSubstitute para criar um substituto da classe.
        return Substitute.For<ServiceBusMessageActions>();
    }
}