using FluentAssertions;
using NotificationProcessor.Application.Models;
using NotificationProcessor.Application.Services;

namespace NotificationProcessor.Tests;

public class NotificationRequestValidatorTests
{
    private readonly NotificationRequestValidator _validator = new();

    [Fact]
    public async Task Validate_ValidEmailNotification_ShouldPass()
    {
        var request = new NotificationRequest
        {
            Type = "email",
            Recipient = "usuario@exemplo.com",
            Subject = "Bem-vindo!",
            Body = "Seu cadastro foi realizado com sucesso."
        };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmailTypeWithInvalidEmail_ShouldFail()
    {
        var request = new NotificationRequest
        {
            Type = "email",
            Recipient = "isso-nao-e-um-email",
            Subject = "Teste",
            Body = "Corpo"
        };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Recipient");
    }

    [Theory]
    [InlineData("")]
    [InlineData("whatsapp")]
    [InlineData("telegram")]
    public async Task Validate_InvalidType_ShouldFail(string type)
    {
        var request = new NotificationRequest
        {
            Type = type,
            Recipient = "usuario@exemplo.com",
            Subject = "Teste",
            Body = "Corpo"
        };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Type");
    }

    [Theory]
    [InlineData("email")]
    [InlineData("push")]
    [InlineData("sms")]
    public async Task Validate_AllAllowedTypes_ShouldPass(string type)
    {
        var request = new NotificationRequest
        {
            Type = type,
            Recipient = type == "email" ? "usuario@exemplo.com" : "device-token-123",
            Subject = "Teste",
            Body = "Corpo da mensagem"
        };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }
}