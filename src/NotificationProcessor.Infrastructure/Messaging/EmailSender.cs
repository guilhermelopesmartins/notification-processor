using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace NotificationProcessor.Infrastructure.Messaging;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
}

public class SendGridEmailSender : IEmailSender
{
    private readonly string _apiKey;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly ILogger<SendGridEmailSender> _logger;

    public SendGridEmailSender(string apiKey, string fromEmail, string fromName, ILogger<SendGridEmailSender> logger)
    {
        _apiKey = apiKey;
        _fromEmail = fromEmail;
        _fromName = fromName;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        var client = new SendGridClient(_apiKey);

        var message = new SendGridMessage
        {
            From = new EmailAddress(_fromEmail, _fromName),
            Subject = subject,
            PlainTextContent = body,
            HtmlContent = $"<p>{body}</p>"
        };

        message.AddTo(new EmailAddress(to));

        var response = await client.SendEmailAsync(message, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation(
                "Email sent successfully. To={To}, Subject={Subject}",
                to, subject);
        }
        else
        {
            var responseBody = await response.Body.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Failed to send email. To={To}, StatusCode={StatusCode}, Response={Response}",
                to, response.StatusCode, responseBody);

            throw new InvalidOperationException($"SendGrid returned {response.StatusCode}: {responseBody}");
        }
    }
}