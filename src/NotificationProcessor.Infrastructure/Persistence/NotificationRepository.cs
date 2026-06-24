using System.Xml.XPath;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using NotificationProcessor.Application.Interfaces;
using NotificationProcessor.Application.Models;

namespace NotificationProcesso.Infrastructure.Persistence;

public class NotificationRepository : INotificationRepository
{
    private readonly string _connectionString;
    private readonly ILogger<NotificationRepository> _logger;

    public NotificationRepository(string connectionString, ILogger<NotificationRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    private SqlConnection CreateConnection() => new(_connectionString);

    public async Task<bool> ExistsAsync(string messageId, CancellationToken cancellationToken = default)
    {
        await using var conn = CreateConnection();
        var result = await conn.ExecuteScalarAsync<int>(
            "usp_Notifications_Exists",
            new { MessageId = messageId },
            commandType: System.Data.CommandType.StoredProcedure  
        );
        
        return result > 0;
    }

    public async Task InsertAsync(NotificationRecord record, CancellationToken cancellationToken = default)
    {
        await using var conn = CreateConnection();
        var id = await conn.ExecuteScalarAsync<int>(
            "usp_Notifications_Insert",
            new
            {
                record.MessageId,
                record.CorrelationId,
                record.Type,
                record.Recipient,
                record.Subject,
                record.Body,
                record.Status,
                record.CreatedAt
            },
            commandType: System.Data.CommandType.StoredProcedure
        );

        record.Id = id;

        _logger.LogInformation(
            "Notification inserted. Id={Id}, MessageId={MessageId}",
            id, record.MessageId  
        );
    }

    public async Task UpdateStatusAsync(string messageId, string status, CancellationToken cancellationToken = default)
    {
        await using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "usp_Notifications_UpdateStatus",
            new { MessageId = messageId, Status = status, ProcessedAt = DateTime.UtcNow },
            commandType: System.Data.CommandType.StoredProcedure
        );
    }

    public async Task<IEnumerable<NotificationRecord>> GetOlderThanAsync(int days, CancellationToken cancellationToken = default)
    {
        await using var conn = CreateConnection();
        return await conn.QueryAsync<NotificationRecord>(
            "usp_Notifications_GetOlderThan",
            new { CutoffDate = DateTime.UtcNow.AddDays(-days) },
            commandType: System.Data.CommandType.StoredProcedure);
    }

    public async Task ArchiveBatchAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        var xmlIds = new System.Text.StringBuilder("<ids>");
        foreach (var id in ids)
            xmlIds.Append($"<id>{id}</id>");
        xmlIds.Append("</ids>");

        await using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "usp_Notifications_ArchiveBatch",
            new { Ids = xmlIds.ToString(), ArchivedAt = DateTime.UtcNow },
            commandType: System.Data.CommandType.StoredProcedure);
    }
}