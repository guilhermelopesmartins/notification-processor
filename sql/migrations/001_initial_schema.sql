-- =============================================================
-- 001_initial_schema.sql
-- Criação do schema inicial do NotificationProcessor
-- =============================================================

CREATE DATABASE NotificationProcessorDb;
GO

USE NotificationProcessorDb;
GO

-- -------------------------------------------------------------
-- Tabela principal
-- -------------------------------------------------------------
CREATE TABLE Notifications
(
    Id            INT IDENTITY(1,1) PRIMARY KEY,
    MessageId     NVARCHAR(64)  NOT NULL,
    CorrelationId NVARCHAR(64)  NOT NULL,
    [Type]        NVARCHAR(20)  NOT NULL,
    Recipient     NVARCHAR(256) NOT NULL,
    Subject       NVARCHAR(512) NOT NULL,
    Body          NVARCHAR(MAX) NOT NULL,
    Status        NVARCHAR(20)  NOT NULL DEFAULT 'Pending',
    CreatedAt     DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
    ProcessedAt   DATETIME2     NULL,
    ArchivedAt    DATETIME2     NULL
);
GO

-- Índice único no MessageId — base da idempotência
CREATE UNIQUE INDEX UX_Notifications_MessageId
    ON Notifications (MessageId);
GO

-- Índice para a query do cleanup (busca por data + status)
CREATE INDEX IX_Notifications_Status_CreatedAt
    ON Notifications (Status, CreatedAt);
GO

-- -------------------------------------------------------------
-- Stored Procedures
-- -------------------------------------------------------------

CREATE OR ALTER PROCEDURE usp_Notifications_Exists
    @MessageId NVARCHAR(64)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(1) FROM Notifications WHERE MessageId = @MessageId;
END;
GO

CREATE OR ALTER PROCEDURE usp_Notifications_Insert
    @MessageId     NVARCHAR(64),
    @CorrelationId NVARCHAR(64),
    @Type          NVARCHAR(20),
    @Recipient     NVARCHAR(256),
    @Subject       NVARCHAR(512),
    @Body          NVARCHAR(MAX),
    @Status        NVARCHAR(20),
    @CreatedAt     DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO Notifications (MessageId, CorrelationId, [Type], Recipient, Subject, Body, Status, CreatedAt)
    VALUES (@MessageId, @CorrelationId, @Type, @Recipient, @Subject, @Body, @Status, @CreatedAt);

    SELECT SCOPE_IDENTITY();
END;
GO

CREATE OR ALTER PROCEDURE usp_Notifications_UpdateStatus
    @MessageId   NVARCHAR(64),
    @Status      NVARCHAR(20),
    @ProcessedAt DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE Notifications
    SET Status = @Status, ProcessedAt = @ProcessedAt
    WHERE MessageId = @MessageId;
END;
GO

CREATE OR ALTER PROCEDURE usp_Notifications_GetOlderThan
    @CutoffDate DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id, MessageId, CorrelationId, [Type], Recipient, Subject, Body,
           Status, CreatedAt, ProcessedAt, ArchivedAt
    FROM Notifications
    WHERE Status = 'Processed'
      AND CreatedAt < @CutoffDate;
END;
GO

SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE usp_Notifications_ArchiveBatch
    @Ids        XML,
    @ArchivedAt DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE Notifications
    SET Status = 'Archived', ArchivedAt = @ArchivedAt
    WHERE Id IN (
        SELECT n.value('.', 'INT')
        FROM @Ids.nodes('//id') AS t(n)
    );
END;
GO