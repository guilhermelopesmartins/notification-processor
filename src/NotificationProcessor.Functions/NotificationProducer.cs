using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using NotificationProcessor.Application.Interfaces;
using NotificationProcessor.Application.Models;

namespace NotificationProcessor.Functions;

public class NotificationProducer
{
    private readonly INotificationPublisher _publisher;
    private readonly IValidator<NotificationRequest> _validator;
    private readonly ILogger<NotificationProducer> _logger;

    public NotificationProducer(
        INotificationPublisher publisher,
        IValidator<NotificationRequest> validator,
        ILogger<NotificationProducer> logger)
    {
        _publisher = publisher;
        _validator = validator;
        _logger = logger;
    }

    [Function("NotificationProducer")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "notifications")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        // Gera o CorrelationId — vai rastrear essa requisição por todo o sistema
        var correlationId = req.Headers.TryGetValues("Correlation-Id", out var values)
            ? values.First()
            : Guid.NewGuid().ToString();

        _logger.LogInformation(
            "Request received. CorrelationId={CorrelationId}",
            correlationId);

        // Desserializa o payload
        NotificationRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<NotificationRequest>(
                req.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("Invalid payload. CorrelationId={CorrelationId}, Error={Error}",
                correlationId, ex.Message);

            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            badRequest.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await badRequest.WriteStringAsync(
                JsonSerializer.Serialize(new { error = "Payload JSON inválido." }), cancellationToken);
            return badRequest;
        }

        if (request is null)
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "Body não pode ser vazio." }, cancellationToken);
            return badRequest;
        }

        // Valida o payload
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => new { field = e.PropertyName, error = e.ErrorMessage });

            _logger.LogWarning(
                "Payload inválido. CorrelationId={CorrelationId}, Errors={Errors}",
                correlationId,
                string.Join(", ", validation.Errors.Select(e => e.ErrorMessage)));

            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            badRequest.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await badRequest.WriteStringAsync(
                JsonSerializer.Serialize(new { errors }), cancellationToken);
            return badRequest;
        }

        // Monta a mensagem e publica no Service Bus
        var message = new NotificationMessage
        {
            MessageId = Guid.NewGuid().ToString(),
            CorrelationId = correlationId,
            Notification = request,
            CreatedAt = DateTime.UtcNow
        };

        await _publisher.PublishAsync(message, cancellationToken);

        _logger.LogInformation(
            "Mensagem publicada. MessageId={MessageId}, CorrelationId={CorrelationId}",
            message.MessageId,
            correlationId);

        // 202 Accepted — a requisição foi aceita mas ainda não processada
        var response = req.CreateResponse(HttpStatusCode.Accepted);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(
            JsonSerializer.Serialize(new
            {
                messageId = message.MessageId,
                correlationId = message.CorrelationId
            }), cancellationToken);

        return response;
    }
}