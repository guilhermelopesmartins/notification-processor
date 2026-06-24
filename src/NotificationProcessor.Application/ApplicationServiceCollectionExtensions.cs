using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using NotificationProcessor.Application.Services;

namespace NotificationProcessor.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<NotificationRequestValidator>();
        return services;
    }
}