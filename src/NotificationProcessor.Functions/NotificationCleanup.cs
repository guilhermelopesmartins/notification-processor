using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using NotificationProcessor.Application.Interfaces;

namespace NotificationProcessor.Functions;

public class NotificationCleanup
{
    private readonly INotificationRepository _repository;
    private readonly ILogger<NotificationCleanup> _logger;

    public NotificationCleanup(
        INotificationRepository repository,
        ILogger<NotificationCleanup> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [Function("NotificationCleanup")]
    public async Task Run(
        [TimerTrigger("0 0 3 * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "NotificationCleanup iniciado. ScheduleStatus={ScheduleStatus}",
            timer.ScheduleStatus?.Last);

        // Busca notificações processadas com mais de 30 dias
        var oldNotifications = await _repository.GetOlderThanAsync(30, cancellationToken);
        var notifications = oldNotifications.ToList();

        if (!notifications.Any())
        {
            _logger.LogInformation("Nenhuma notificação para arquivar.");
            return;
        }

        _logger.LogInformation(
            "Encontradas {Count} notificações para arquivar.",
            notifications.Count);

        // Arquiva em lote
        var ids = notifications.Select(n => n.Id).ToList();
        await _repository.ArchiveBatchAsync(ids, cancellationToken);

        _logger.LogInformation(
            "Arquivamento concluído. {Count} notificações arquivadas.",
            notifications.Count);
    }
}