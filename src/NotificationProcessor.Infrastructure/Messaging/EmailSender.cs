using System.Net;
using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Logging;
using Azure.Communication.Email;

namespace NotificationProcessor.Infrastructure.Messaging;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
}

public class AzureEmailSender : IEmailSender
{
    private readonly EmailClient _client;
    private readonly string _senderAddress;
    private readonly ILogger<AzureEmailSender> _logger;

    public AzureEmailSender(EmailClient client, string senderAddress, ILogger<AzureEmailSender> logger)
    {
        _client = client;
        _senderAddress = senderAddress;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        var content = new EmailContent(subject)
        {
            PlainText = body,
            // Codifica o texto para evitar que HTML enviado no body seja interpretado no email
            Html = $"<p>{WebUtility.HtmlEncode(body)}</p>"
        };

        var message = new EmailMessage(_senderAddress, to, content);

        // WaitUntil.Completed aguarda o serviço confirmar o resultado do envio
        EmailSendOperation operation = await _client.SendAsync(WaitUntil.Completed, message, cancellationToken);

        if (operation.Value.Status != EmailSendStatus.Succeeded)
        {
            _logger.LogError(
                "Failed to send email. To={To}, Status={Status}, OperationId={OperationId}",
                to, operation.Value.Status, operation.Id);

            throw new InvalidOperationException($"Email send failed with status {operation.Value.Status}");
        }

        _logger.LogInformation(
            "Email sent successfully. To={To}, OperationId={OperationId}",
            to, operation.Id);
    }
}