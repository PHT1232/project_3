-- =============================================================================
-- Stationery Request Management - Schema
-- Generated from the ER diagram in __ai_agents/systemprompt.md discussion.
-- Target: SQL Server (matches backend.md: .NET 10 / EF Core, nvarchar/datetime2/rowversion)
-- =============================================================================

-- -----------------------------------------------------------------------------
-- ROLES
-- -----------------------------------------------------------------------------
CREATE TABLE [Roles] (
  [RoleId]     INT IDENTITY(1,1) NOT NULL,
  [RoleName]   NVARCHAR(100)     NOT NULL,
  [RankLevel]  INT               NOT NULL,
  CONSTRAINT [PK_Roles] PRIMARY KEY ([RoleId]),
  CONSTRAINT [UQ_Roles_RoleName] UNIQUE ([RoleName])
)
GO

-- -----------------------------------------------------------------------------
-- ROLE_THRESHOLDS (1:1 with Roles - shares the same key)
-- -----------------------------------------------------------------------------
CREATE TABLE [RoleThresholds] (
  [RoleId]              INT             NOT NULL,
  [MaxAmountPerRequest]  DECIMAL(18,2)   NOT NULL,
  [MaxAmountPerMonth]    DECIMAL(18,2)   NOT NULL,
  CONSTRAINT [PK_RoleThresholds] PRIMARY KEY ([RoleId])
)
GO

-- -----------------------------------------------------------------------------
-- USERS (self-referencing hierarchy via SuperiorEmployeeNumber)
-- -----------------------------------------------------------------------------
CREATE TABLE [Users] (
  [EmployeeNumber]           INT IDENTITY(1,1) NOT NULL,
  [Name]                     NVARCHAR(200)     NOT NULL,
  [RoleId]                   INT               NOT NULL,
  [EmailId]                  NVARCHAR(256)     NOT NULL,
  [SuperiorEmployeeNumber]   INT               NULL,
  [PasswordHash]             NVARCHAR(256)     NOT NULL,
  [Grade]                    NVARCHAR(50)      NULL,
  [Location]                 NVARCHAR(100)     NULL,
  [IsActive]                 BIT               NOT NULL CONSTRAINT [DF_Users_IsActive] DEFAULT (1),
  [CreatedAtUtc]             DATETIME2         NOT NULL CONSTRAINT [DF_Users_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT [PK_Users] PRIMARY KEY ([EmployeeNumber]),
  CONSTRAINT [UQ_Users_EmailId] UNIQUE ([EmailId])
)
GO

-- -----------------------------------------------------------------------------
-- SUPPLIERS
-- -----------------------------------------------------------------------------
CREATE TABLE [Suppliers] (
  [SupplierId]     INT IDENTITY(1,1) NOT NULL,
  [Name]           NVARCHAR(200)     NOT NULL,
  [ContactEmail]   NVARCHAR(256)     NULL,
  [Phone]          NVARCHAR(30)      NULL,
  [LeadTimeDays]   INT               NOT NULL CONSTRAINT [DF_Suppliers_LeadTimeDays] DEFAULT (0),
  [IsActive]       BIT               NOT NULL CONSTRAINT [DF_Suppliers_IsActive] DEFAULT (1),
  CONSTRAINT [PK_Suppliers] PRIMARY KEY ([SupplierId])
)
GO

-- -----------------------------------------------------------------------------
-- CATEGORIES
-- -----------------------------------------------------------------------------
CREATE TABLE [Categories] (
  [CategoryId]   INT IDENTITY(1,1) NOT NULL,
  [Name]         NVARCHAR(150)     NOT NULL,
  CONSTRAINT [PK_Categories] PRIMARY KEY ([CategoryId]),
  CONSTRAINT [UQ_Categories_Name] UNIQUE ([Name])
)
GO

-- -----------------------------------------------------------------------------
-- STATIONERY_ITEMS
-- -----------------------------------------------------------------------------
CREATE TABLE [StationeryItems] (
  [ItemId]                  INT IDENTITY(1,1) NOT NULL,
  [ItemName]                NVARCHAR(200)     NOT NULL,
  [CategoryId]              INT               NOT NULL,
  [SupplierId]              INT               NOT NULL,
  [UnitCost]                DECIMAL(18,2)     NOT NULL,
  [QuantityAvailable]       INT               NOT NULL CONSTRAINT [DF_StationeryItems_QuantityAvailable] DEFAULT (0),
  [ReorderLevel]            INT               NOT NULL CONSTRAINT [DF_StationeryItems_ReorderLevel] DEFAULT (0),
  [MinRankLevelToRequest]   INT               NOT NULL CONSTRAINT [DF_StationeryItems_MinRankLevelToRequest] DEFAULT (0),
  [IsActive]                BIT               NOT NULL CONSTRAINT [DF_StationeryItems_IsActive] DEFAULT (1),
  [RowVersion]              ROWVERSION        NOT NULL,
  CONSTRAINT [PK_StationeryItems] PRIMARY KEY ([ItemId]),
  CONSTRAINT [CK_StationeryItems_QuantityAvailable] CHECK ([QuantityAvailable] >= 0),
  CONSTRAINT [CK_StationeryItems_ReorderLevel] CHECK ([ReorderLevel] >= 0)
)
GO

-- -----------------------------------------------------------------------------
-- REQUESTS
-- Status values are an assumption -- adjust the CHECK constraint to match
-- your actual approval workflow.
-- -----------------------------------------------------------------------------
CREATE TABLE [Requests] (
  [RequestId]                  INT IDENTITY(1,1) NOT NULL,
  [RequestorEmployeeNumber]    INT               NOT NULL,
  [ApproverEmployeeNumber]     INT               NULL,
  [Status]                     NVARCHAR(30)      NOT NULL CONSTRAINT [DF_Requests_Status] DEFAULT ('Pending'),
  [RequiredByDate]             DATE              NULL,
  [TotalEstimatedCost]         DECIMAL(18,2)     NOT NULL CONSTRAINT [DF_Requests_TotalEstimatedCost] DEFAULT (0),
  [DecisionComment]            NVARCHAR(1000)    NULL,
  [CreatedAtUtc]                DATETIME2         NOT NULL CONSTRAINT [DF_Requests_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
  [DecidedAtUtc]                DATETIME2         NULL,
  [RowVersion]                 ROWVERSION        NOT NULL,
  CONSTRAINT [PK_Requests] PRIMARY KEY ([RequestId]),
  CONSTRAINT [CK_Requests_Status] CHECK ([Status] IN ('Pending', 'Approved', 'Rejected', 'PartiallyApproved', 'Cancelled', 'Fulfilled'))
)
GO

-- -----------------------------------------------------------------------------
-- REQUEST_ITEMS
-- LineTotal is stored (per the diagram) rather than computed; keep it in
-- sync with Quantity * UnitCostSnapshot in the Application layer.
-- -----------------------------------------------------------------------------
CREATE TABLE [RequestItems] (
  [RequestItemId]      INT IDENTITY(1,1) NOT NULL,
  [RequestId]           INT               NOT NULL,
  [ItemId]              INT               NOT NULL,
  [Quantity]            INT               NOT NULL,
  [UnitCostSnapshot]    DECIMAL(18,2)     NOT NULL,
  [LineTotal]           DECIMAL(18,2)     NOT NULL,
  CONSTRAINT [PK_RequestItems] PRIMARY KEY ([RequestItemId]),
  CONSTRAINT [CK_RequestItems_Quantity] CHECK ([Quantity] > 0)
)
GO

-- -----------------------------------------------------------------------------
-- REQUEST_STATUS_HISTORY
-- FromStatus is nullable to allow logging the initial "created" transition.
-- -----------------------------------------------------------------------------
CREATE TABLE [RequestStatusHistory] (
  [HistoryId]              BIGINT IDENTITY(1,1) NOT NULL,
  [RequestId]              INT                   NOT NULL,
  [FromStatus]             NVARCHAR(30)          NULL,
  [ToStatus]               NVARCHAR(30)          NOT NULL,
  [ActorEmployeeNumber]    INT                   NOT NULL,
  [Comment]                NVARCHAR(1000)        NULL,
  [CreatedAtUtc]           DATETIME2             NOT NULL CONSTRAINT [DF_RequestStatusHistory_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT [PK_RequestStatusHistory] PRIMARY KEY ([HistoryId])
)
GO

-- -----------------------------------------------------------------------------
-- NOTIFICATIONS
-- RequestId is nullable -- not every notification necessarily relates to a request.
-- -----------------------------------------------------------------------------
CREATE TABLE [Notifications] (
  [NotificationId]             BIGINT IDENTITY(1,1) NOT NULL,
  [RecipientEmployeeNumber]    INT                   NOT NULL,
  [RequestId]                  INT                   NULL,
  [EventType]                  NVARCHAR(50)          NOT NULL,
  [Title]                      NVARCHAR(200)         NOT NULL,
  [Message]                    NVARCHAR(1000)        NOT NULL,
  [IsRead]                     BIT                   NOT NULL CONSTRAINT [DF_Notifications_IsRead] DEFAULT (0),
  [CreatedAtUtc]               DATETIME2             NOT NULL CONSTRAINT [DF_Notifications_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT [PK_Notifications] PRIMARY KEY ([NotificationId])
)
GO

-- -----------------------------------------------------------------------------
-- STOCK_TRANSACTIONS
-- ChangeQuantity is signed (positive = stock in, negative = stock out).
-- RequestId is nullable to allow manual restocks/adjustments unrelated to a request.
-- TxType values are an assumption -- adjust to match your workflow.
-- -----------------------------------------------------------------------------
CREATE TABLE [StockTransactions] (
  [StockTxId]              BIGINT IDENTITY(1,1) NOT NULL,
  [ItemId]                 INT                   NOT NULL,
  [ChangeQuantity]         INT                   NOT NULL,
  [TxType]                 NVARCHAR(30)          NOT NULL,
  [RequestId]              INT                   NULL,
  [ActorEmployeeNumber]    INT                   NOT NULL,
  [CreatedAtUtc]           DATETIME2             NOT NULL CONSTRAINT [DF_StockTransactions_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT [PK_StockTransactions] PRIMARY KEY ([StockTxId]),
  CONSTRAINT [CK_StockTransactions_TxType] CHECK ([TxType] IN ('Inbound', 'Outbound', 'Adjustment', 'Return'))
)
GO

-- -----------------------------------------------------------------------------
-- AI_INTERACTION_LOGS
-- -----------------------------------------------------------------------------
CREATE TABLE [AiInteractionLogs] (
  [LogId]              BIGINT IDENTITY(1,1) NOT NULL,
  [EmployeeNumber]     INT                   NOT NULL,
  [Feature]            NVARCHAR(100)         NOT NULL,
  [PromptSummary]      NVARCHAR(1000)        NULL,
  [ResponseSummary]    NVARCHAR(1000)        NULL,
  [ModelName]          NVARCHAR(100)         NULL,
  [LatencyMs]          INT                   NULL,
  [WasFallback]        BIT                   NOT NULL CONSTRAINT [DF_AiInteractionLogs_WasFallback] DEFAULT (0),
  [CreatedAtUtc]       DATETIME2             NOT NULL CONSTRAINT [DF_AiInteractionLogs_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT [PK_AiInteractionLogs] PRIMARY KEY ([LogId])
)
GO

-- =============================================================================
-- Foreign Keys
-- All FKs pointing at [Users] use NO ACTION on delete: with five different
-- FK paths converging on Users (superior, requestor, approver, recipient,
-- actor, invoker), SQL Server rejects multiple cascade paths anyway, and
-- cascading deletes through employee history is rarely desirable -- prefer
-- the IsActive flag for deactivating users instead of deleting rows.
-- =============================================================================

ALTER TABLE [RoleThresholds]
  ADD CONSTRAINT [FK_RoleThresholds_Roles] FOREIGN KEY ([RoleId])
      REFERENCES [Roles] ([RoleId])
GO

ALTER TABLE [Users]
  ADD CONSTRAINT [FK_Users_Roles] FOREIGN KEY ([RoleId])
      REFERENCES [Roles] ([RoleId])
GO

ALTER TABLE [Users]
  ADD CONSTRAINT [FK_Users_Superior] FOREIGN KEY ([SuperiorEmployeeNumber])
      REFERENCES [Users] ([EmployeeNumber])
GO

ALTER TABLE [StationeryItems]
  ADD CONSTRAINT [FK_StationeryItems_Categories] FOREIGN KEY ([CategoryId])
      REFERENCES [Categories] ([CategoryId])
GO

ALTER TABLE [StationeryItems]
  ADD CONSTRAINT [FK_StationeryItems_Suppliers] FOREIGN KEY ([SupplierId])
      REFERENCES [Suppliers] ([SupplierId])
GO

ALTER TABLE [Requests]
  ADD CONSTRAINT [FK_Requests_Requestor] FOREIGN KEY ([RequestorEmployeeNumber])
      REFERENCES [Users] ([EmployeeNumber])
GO

ALTER TABLE [Requests]
  ADD CONSTRAINT [FK_Requests_Approver] FOREIGN KEY ([ApproverEmployeeNumber])
      REFERENCES [Users] ([EmployeeNumber])
GO

ALTER TABLE [RequestItems]
  ADD CONSTRAINT [FK_RequestItems_Requests] FOREIGN KEY ([RequestId])
      REFERENCES [Requests] ([RequestId])
      ON DELETE CASCADE
GO

ALTER TABLE [RequestItems]
  ADD CONSTRAINT [FK_RequestItems_StationeryItems] FOREIGN KEY ([ItemId])
      REFERENCES [StationeryItems] ([ItemId])
GO

ALTER TABLE [RequestStatusHistory]
  ADD CONSTRAINT [FK_RequestStatusHistory_Requests] FOREIGN KEY ([RequestId])
      REFERENCES [Requests] ([RequestId])
      ON DELETE CASCADE
GO

ALTER TABLE [RequestStatusHistory]
  ADD CONSTRAINT [FK_RequestStatusHistory_Actor] FOREIGN KEY ([ActorEmployeeNumber])
      REFERENCES [Users] ([EmployeeNumber])
GO

ALTER TABLE [Notifications]
  ADD CONSTRAINT [FK_Notifications_Recipient] FOREIGN KEY ([RecipientEmployeeNumber])
      REFERENCES [Users] ([EmployeeNumber])
GO

ALTER TABLE [Notifications]
  ADD CONSTRAINT [FK_Notifications_Requests] FOREIGN KEY ([RequestId])
      REFERENCES [Requests] ([RequestId])
GO

ALTER TABLE [StockTransactions]
  ADD CONSTRAINT [FK_StockTransactions_StationeryItems] FOREIGN KEY ([ItemId])
      REFERENCES [StationeryItems] ([ItemId])
GO

ALTER TABLE [StockTransactions]
  ADD CONSTRAINT [FK_StockTransactions_Requests] FOREIGN KEY ([RequestId])
      REFERENCES [Requests] ([RequestId])
GO

ALTER TABLE [StockTransactions]
  ADD CONSTRAINT [FK_StockTransactions_Actor] FOREIGN KEY ([ActorEmployeeNumber])
      REFERENCES [Users] ([EmployeeNumber])
GO

ALTER TABLE [AiInteractionLogs]
  ADD CONSTRAINT [FK_AiInteractionLogs_Users] FOREIGN KEY ([EmployeeNumber])
      REFERENCES [Users] ([EmployeeNumber])
GO
