using FluentValidation;
using NotificationProcessor.Application.Models;

namespace NotificationProcessor.Application.Services;

public class NotificationRequestValidator : AbstractValidator<NotificationRequest>
{
    private static readonly string[] AllowedTypes = ["email", "push", "sms"];

    public NotificationRequestValidator()
    {
        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("Type is required.")
            .Must(t => AllowedTypes.Contains(t.ToLower()))
            .WithMessage($"Type should be one of the values: {string.Join(", ", AllowedTypes)}.");

        RuleFor(x => x.Recipient)
            .NotEmpty().WithMessage("Recipient is required.")
            .MaximumLength(256).WithMessage("Recipient too large.");

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Subject is required.")
            .MaximumLength(512).WithMessage("Subject too large.");

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Body is required.");

        When(x => x.Type?.ToLower() == "email", () =>
        {
            RuleFor(x => x.Recipient)
                .EmailAddress().WithMessage("Recipient should be a valid email for notifications with email type");
        });
    }
}