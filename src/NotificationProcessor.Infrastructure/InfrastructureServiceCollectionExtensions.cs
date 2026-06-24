using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NotificationProcesso.Infrastructure.Persistence;
using NotificationProcessor.Application.Interfaces;

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

        return services;
    }
}