using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NotificationProcessor.Infrastructure.Persistence;
using NotificationProcessor.Application.Interfaces;
using NotificationProcessor.Infrastructure.Messaging;

namespace NotificationProcessor.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton(_ =>
            new ServiceBusClient(configuration["ServiceBusConnectionString"]));

        services.AddSingleton<INotificationPublisher>(sp =>
            new ServiceBusNotificationPublisher(
                sp.GetRequiredService<ServiceBusClient>(),
                configuration["ServiceBusQueueName"] ?? "notifications",
                sp.GetRequiredService<ILogger<ServiceBusNotificationPublisher>>()));

        services.AddTransient<INotificationRepository>(sp =>
            new NotificationRepository(
                configuration["SqlConnectionString"]
                    ?? throw new InvalidOperationException("SqlConnectionString não configurada."),
                sp.GetRequiredService<ILogger<NotificationRepository>>()));

        services.AddTransient<IEmailSender>(sp =>
            new SendGridEmailSender(
                configuration["SendGridApiKey"]
                    ?? throw new InvalidOperationException("SendGridApiKey não configurada."),
                configuration["SendGridFromEmail"]
                    ?? throw new InvalidOperationException("SendGridFromEmail não configurada."),
                configuration["SendGridFromName"] ?? "Notification Processor",
                sp.GetRequiredService<ILogger<SendGridEmailSender>>()));

        return services;
    }
}