using Azure.Core;
using Azure.Identity;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using NotificationProcessor.Application.Interfaces;
using NotificationProcessor.Application.Models;

namespace NotificationProcessor.Infrastructure.Persistence;

public class NotificationRepository : INotificationRepository
{
    private readonly string _connectionString;
    private readonly bool _useManagedIdentity;
    private readonly ILogger<NotificationRepository> _logger;

    public NotificationRepository(string connectionString, ILogger<NotificationRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;

        // Detecta se deve usar Managed Identity baseado na connection string
        _useManagedIdentity = connectionString.Contains("Active Directory Default", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<SqlConnection> CreateConnectionAsync()
    {
        // Remove Authentication da connection string — vamos injetar o token manualmente
        var builder = new SqlConnectionStringBuilder(_connectionString);
        builder.Remove("Authentication");
        var conn = new SqlConnection(builder.ConnectionString);

        if (_useManagedIdentity)
        {
            var credential = new DefaultAzureCredential();
            var tokenRequestContext = new TokenRequestContext(["https://database.windows.net/.default"]);
            var token = await credential.GetTokenAsync(tokenRequestContext);
            conn.AccessToken = token.Token;
        }

        return conn;
    }

    public async Task<bool> ExistsAsync(string messageId, CancellationToken cancellationToken = default)
    {
        await using var conn = await CreateConnectionAsync();
        var result = await conn.ExecuteScalarAsync<int>(
            "usp_Notifications_Exists",
            new { MessageId = messageId },
            commandType: System.Data.CommandType.StoredProcedure);

        return result > 0;
    }

    public async Task InsertAsync(NotificationRecord record, CancellationToken cancellationToken = default)
    {
        await using var conn = await CreateConnectionAsync();
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
            commandType: System.Data.CommandType.StoredProcedure);

        record.Id = id;

        _logger.LogInformation(
            "Notification inserted. Id={Id}, MessageId={MessageId}",
            id, record.MessageId);
    }

    public async Task UpdateStatusAsync(string messageId, string status, CancellationToken cancellationToken = default)
    {
        await using var conn = await CreateConnectionAsync();
        await conn.ExecuteAsync(
            "usp_Notifications_UpdateStatus",
            new { MessageId = messageId, Status = status, ProcessedAt = DateTime.UtcNow },
            commandType: System.Data.CommandType.StoredProcedure);
    }

    public async Task<IEnumerable<NotificationRecord>> GetOlderThanAsync(int days, CancellationToken cancellationToken = default)
    {
        await using var conn = await CreateConnectionAsync();
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

        await using var conn = await CreateConnectionAsync();
        await conn.ExecuteAsync(
            "usp_Notifications_ArchiveBatch",
            new { Ids = xmlIds.ToString(), ArchivedAt = DateTime.UtcNow },
            commandType: System.Data.CommandType.StoredProcedure);
    }
}