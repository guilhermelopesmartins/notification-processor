using Azure.Communication.Email;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NotificationProcessor.Application.Interfaces;
using NotificationProcessor.Infrastructure.Messaging;
using NotificationProcessor.Infrastructure.Persistence;

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

        services.AddSingleton(_ =>
            new EmailClient(configuration["AcsConnectionString"]
                ?? throw new InvalidOperationException("AcsConnectionString não configurada.")));

        services.AddTransient<IEmailSender>(sp =>
            new AzureEmailSender(
                sp.GetRequiredService<EmailClient>(),
                configuration["AcsSenderAddress"]
                    ?? throw new InvalidOperationException("AcsSenderAddress não configurada."),
                sp.GetRequiredService<ILogger<AzureEmailSender>>()));

        return services;
    }
}