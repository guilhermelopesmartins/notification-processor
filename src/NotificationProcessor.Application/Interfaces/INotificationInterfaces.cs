using NotificationProcessor.Application.Models;

namespace NotificationProcessor.Application.Interfaces;

public interface INotificationPublisher
{
    Task PublishAsync(NotificationMessage message, CancellationToken cancellationToken = default);
}

public interface INotificationRepository
{
    Task<bool> ExistsAsync(string messageId, CancellationToken cancellationToken = default);
    Task InsertAsync(NotificationRecord record, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(string messageId, string status, CancellationToken cancellationToken = default);
    Task<IEnumerable<NotificationRecord>> GetOlderThanAsync(int days, CancellationToken cancellationToken = default);
    Task ArchiveBatchAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);
}