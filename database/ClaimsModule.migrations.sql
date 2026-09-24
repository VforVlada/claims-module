IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    CREATE SEQUENCE [dbo].[ClaimNumberSequence] START WITH 1 INCREMENT BY 1 NO CYCLE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    CREATE TABLE [CauseOfLossCodes] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [Description] nvarchar(255) NOT NULL,
        [PerilCategory] nvarchar(50) NOT NULL,
        [IsActive] bit NOT NULL,
        [OrganizationEntityId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [UserCreated] nvarchar(255) NOT NULL,
        [UserModified] nvarchar(255) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetimeoffset NULL,
        CONSTRAINT [PK_CauseOfLossCodes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    CREATE TABLE [ClaimAuditLogs] (
        [Id] uniqueidentifier NOT NULL,
        [ClaimId] uniqueidentifier NOT NULL,
        [Action] nvarchar(100) NOT NULL,
        [OldValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        [PerformedBy] nvarchar(255) NOT NULL,
        [OrganizationEntityId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [UserCreated] nvarchar(255) NOT NULL,
        [UserModified] nvarchar(255) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetimeoffset NULL,
        CONSTRAINT [PK_ClaimAuditLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    CREATE TABLE [ClaimStatusTransitions] (
        [Id] uniqueidentifier NOT NULL,
        [FromStatus] nvarchar(50) NOT NULL,
        [ToStatus] nvarchar(50) NOT NULL,
        [RequiredPermission] nvarchar(50) NOT NULL,
        [OrganizationEntityId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [UserCreated] nvarchar(255) NOT NULL,
        [UserModified] nvarchar(255) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetimeoffset NULL,
        CONSTRAINT [PK_ClaimStatusTransitions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    CREATE TABLE [Policies] (
        [Id] uniqueidentifier NOT NULL,
        [PolicyNumber] nvarchar(50) NOT NULL,
        [ClientName] nvarchar(255) NOT NULL,
        [EffectiveDate] datetimeoffset NOT NULL,
        [ExpirationDate] datetimeoffset NOT NULL,
        [OrganizationEntityId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [UserCreated] nvarchar(255) NOT NULL,
        [UserModified] nvarchar(255) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetimeoffset NULL,
        CONSTRAINT [PK_Policies] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    CREATE TABLE [Claims] (
        [Id] uniqueidentifier NOT NULL,
        [ClaimNumber] nvarchar(20) NOT NULL,
        [PolicyId] uniqueidentifier NULL,
        [ClaimType] nvarchar(50) NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [AssignedHandler] nvarchar(255) NOT NULL,
        [RequiresManagerOverride] bit NOT NULL,
        [OrganizationEntityId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [UserCreated] nvarchar(255) NOT NULL,
        [UserModified] nvarchar(255) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetimeoffset NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_Claims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Claims_Policies_PolicyId] FOREIGN KEY ([PolicyId]) REFERENCES [Policies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    CREATE TABLE [PolicyCoverages] (
        [Id] uniqueidentifier NOT NULL,
        [PolicyId] uniqueidentifier NOT NULL,
        [CoverageType] nvarchar(100) NOT NULL,
        [DeductibleAmount] decimal(19,4) NOT NULL,
        [DeductibleCurrency] nvarchar(3) NOT NULL,
        [LimitAmount] decimal(19,4) NOT NULL,
        [LimitCurrency] nvarchar(3) NOT NULL,
        [OrganizationEntityId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [UserCreated] nvarchar(255) NOT NULL,
        [UserModified] nvarchar(255) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetimeoffset NULL,
        CONSTRAINT [PK_PolicyCoverages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PolicyCoverages_Policies_PolicyId] FOREIGN KEY ([PolicyId]) REFERENCES [Policies] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    CREATE TABLE [ClaimDocuments] (
        [Id] uniqueidentifier NOT NULL,
        [ClaimId] uniqueidentifier NOT NULL,
        [FileName] nvarchar(500) NOT NULL,
        [SanitizedFileName] nvarchar(500) NOT NULL,
        [ContentType] nvarchar(255) NOT NULL,
        [SizeBytes] bigint NOT NULL,
        [BlobPath] nvarchar(1000) NOT NULL,
        [UploadedBy] nvarchar(255) NOT NULL,
        [OrganizationEntityId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [UserCreated] nvarchar(255) NOT NULL,
        [UserModified] nvarchar(255) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetimeoffset NULL,
        CONSTRAINT [PK_ClaimDocuments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ClaimDocuments_Claims_ClaimId] FOREIGN KEY ([ClaimId]) REFERENCES [Claims] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    CREATE TABLE [ClaimParties] (
        [Id] uniqueidentifier NOT NULL,
        [ClaimId] uniqueidentifier NOT NULL,
        [PartyType] nvarchar(50) NOT NULL,
        [PartyRole] nvarchar(50) NOT NULL,
        [Name] nvarchar(255) NOT NULL,
        [ContactEmail] nvarchar(255) NULL,
        [ContactPhone] nvarchar(50) NULL,
        [OrganizationEntityId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [UserCreated] nvarchar(255) NOT NULL,
        [UserModified] nvarchar(255) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetimeoffset NULL,
        CONSTRAINT [PK_ClaimParties] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ClaimParties_Claims_ClaimId] FOREIGN KEY ([ClaimId]) REFERENCES [Claims] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    CREATE TABLE [ClaimReserveComponents] (
        [Id] uniqueidentifier NOT NULL,
        [ClaimId] uniqueidentifier NOT NULL,
        [ComponentType] nvarchar(50) NOT NULL,
        [CurrentAmount] decimal(19,4) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [OrganizationEntityId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [UserCreated] nvarchar(255) NOT NULL,
        [UserModified] nvarchar(255) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetimeoffset NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_ClaimReserveComponents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ClaimReserveComponents_Claims_ClaimId] FOREIGN KEY ([ClaimId]) REFERENCES [Claims] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    CREATE TABLE [ClaimRiskObjects] (
        [Id] uniqueidentifier NOT NULL,
        [ClaimId] uniqueidentifier NOT NULL,
        [AssetType] nvarchar(50) NOT NULL,
        [Description] nvarchar(1000) NOT NULL,
        [Identifier] nvarchar(255) NULL,
        [OrganizationEntityId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [UserCreated] nvarchar(255) NOT NULL,
        [UserModified] nvarchar(255) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetimeoffset NULL,
        CONSTRAINT [PK_ClaimRiskObjects] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ClaimRiskObjects_Claims_ClaimId] FOREIGN KEY ([ClaimId]) REFERENCES [Claims] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    CREATE TABLE [LossEvents] (
        [Id] uniqueidentifier NOT NULL,
        [ClaimId] uniqueidentifier NOT NULL,
        [LossDate] datetimeoffset NOT NULL,
        [Description] nvarchar(2000) NOT NULL,
        [Location] nvarchar(500) NOT NULL,
        [CauseOfLossCodeId] uniqueidentifier NOT NULL,
        [OrganizationEntityId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [UserCreated] nvarchar(255) NOT NULL,
        [UserModified] nvarchar(255) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetimeoffset NULL,
        CONSTRAINT [PK_LossEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LossEvents_CauseOfLossCodes_CauseOfLossCodeId] FOREIGN KEY ([CauseOfLossCodeId]) REFERENCES [CauseOfLossCodes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LossEvents_Claims_ClaimId] FOREIGN KEY ([ClaimId]) REFERENCES [Claims] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    CREATE TABLE [ReserveHistories] (
        [Id] uniqueidentifier NOT NULL,
        [ReserveComponentId] uniqueidentifier NOT NULL,
        [ChangeSequence] int NOT NULL,
        [ApprovalStatus] nvarchar(50) NOT NULL,
        [PostingStatus] nvarchar(50) NOT NULL,
        [PostingJobId] nvarchar(100) NULL,
        [RejectionReason] nvarchar(1000) NULL,
        [RequestedBy] nvarchar(255) NOT NULL,
        [DecidedBy] nvarchar(255) NULL,
        [DecidedAt] datetimeoffset NULL,
        [Amount] decimal(19,4) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [OrganizationEntityId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [UserCreated] nvarchar(255) NOT NULL,
        [UserModified] nvarchar(255) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetimeoffset NULL,
        CONSTRAINT [PK_ReserveHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReserveHistories_ClaimReserveComponents_ReserveComponentId] FOREIGN KEY ([ReserveComponentId]) REFERENCES [ClaimReserveComponents] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'CreatedAt', N'DeletedAt', N'Description', N'IsActive', N'IsDeleted', N'OrganizationEntityId', N'PerilCategory', N'UpdatedAt', N'UserCreated', N'UserModified') AND [object_id] = OBJECT_ID(N'[CauseOfLossCodes]'))
        SET IDENTITY_INSERT [CauseOfLossCodes] ON;
    EXEC(N'INSERT INTO [CauseOfLossCodes] ([Id], [Code], [CreatedAt], [DeletedAt], [Description], [IsActive], [IsDeleted], [OrganizationEntityId], [PerilCategory], [UpdatedAt], [UserCreated], [UserModified])
    VALUES (''00000000-0000-0000-0000-0000000000c1'', N''COLL'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Collision'', CAST(1 AS bit), CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Auto'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-0000000000c2'', N''COMP'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Comprehensive (non-collision)'', CAST(1 AS bit), CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Auto'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-0000000000c3'', N''THEFT'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Theft'', CAST(1 AS bit), CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Auto'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-0000000000c4'', N''FIRE'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Fire'', CAST(1 AS bit), CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Property'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-0000000000c5'', N''WATER'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Water damage'', CAST(1 AS bit), CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Property'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-0000000000c6'', N''WIND'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Windstorm/hail'', CAST(1 AS bit), CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Property'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-0000000000c7'', N''SLIP'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Slip and fall'', CAST(1 AS bit), CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Liability'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-0000000000c8'', N''PRODLIA'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Product liability'', CAST(1 AS bit), CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Liability'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-0000000000c9'', N''OTHER'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Other / unclassified'', CAST(1 AS bit), CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Other'', NULL, N''seed'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'CreatedAt', N'DeletedAt', N'Description', N'IsActive', N'IsDeleted', N'OrganizationEntityId', N'PerilCategory', N'UpdatedAt', N'UserCreated', N'UserModified') AND [object_id] = OBJECT_ID(N'[CauseOfLossCodes]'))
        SET IDENTITY_INSERT [CauseOfLossCodes] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'DeletedAt', N'FromStatus', N'IsDeleted', N'OrganizationEntityId', N'RequiredPermission', N'ToStatus', N'UpdatedAt', N'UserCreated', N'UserModified') AND [object_id] = OBJECT_ID(N'[ClaimStatusTransitions]'))
        SET IDENTITY_INSERT [ClaimStatusTransitions] ON;
    EXEC(N'INSERT INTO [ClaimStatusTransitions] ([Id], [CreatedAt], [DeletedAt], [FromStatus], [IsDeleted], [OrganizationEntityId], [RequiredPermission], [ToStatus], [UpdatedAt], [UserCreated], [UserModified])
    VALUES (''00000000-0000-0000-0000-000000000101'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Draft'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Handler'', N''Open'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000102'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Draft'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Supervisor'', N''Open'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000103'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Draft'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Manager'', N''Open'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000104'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Draft'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Handler'', N''Closed'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000105'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Draft'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Supervisor'', N''Closed'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000106'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Draft'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Manager'', N''Closed'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000107'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Open'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Handler'', N''UnderInvestigation'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000108'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Open'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Supervisor'', N''UnderInvestigation'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000109'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Open'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Manager'', N''UnderInvestigation'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000110'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Open'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Handler'', N''PendingPayment'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000111'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Open'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Supervisor'', N''PendingPayment'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000112'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Open'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Manager'', N''PendingPayment'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000113'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Open'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Handler'', N''Closed'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000114'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Open'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Supervisor'', N''Closed'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000115'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Open'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Manager'', N''Closed'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000116'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''UnderInvestigation'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Handler'', N''PendingPayment'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000117'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''UnderInvestigation'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Supervisor'', N''PendingPayment'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000118'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''UnderInvestigation'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Manager'', N''PendingPayment'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000119'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''UnderInvestigation'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Handler'', N''Closed'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000120'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''UnderInvestigation'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Supervisor'', N''Closed'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000121'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''UnderInvestigation'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Manager'', N''Closed'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000122'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''PendingPayment'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Handler'', N''Closed'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000123'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''PendingPayment'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Supervisor'', N''Closed'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000124'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''PendingPayment'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Manager'', N''Closed'', NULL, N''seed'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'DeletedAt', N'FromStatus', N'IsDeleted', N'OrganizationEntityId', N'RequiredPermission', N'ToStatus', N'UpdatedAt', N'UserCreated', N'UserModified') AND [object_id] = OBJECT_ID(N'[ClaimStatusTransitions]'))
        SET IDENTITY_INSERT [ClaimStatusTransitions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ClientName', N'CreatedAt', N'DeletedAt', N'EffectiveDate', N'ExpirationDate', N'IsDeleted', N'OrganizationEntityId', N'PolicyNumber', N'UpdatedAt', N'UserCreated', N'UserModified') AND [object_id] = OBJECT_ID(N'[Policies]'))
        SET IDENTITY_INSERT [Policies] ON;
    EXEC(N'INSERT INTO [Policies] ([Id], [ClientName], [CreatedAt], [DeletedAt], [EffectiveDate], [ExpirationDate], [IsDeleted], [OrganizationEntityId], [PolicyNumber], [UpdatedAt], [UserCreated], [UserModified])
    VALUES (''00000000-0000-0000-0000-0000000000a1'', N''Acme Logistics LLC'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, ''2026-01-01T00:00:00.0000000+00:00'', ''2027-01-01T00:00:00.0000000+00:00'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''POL-2026-000101'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-0000000000a2'', N''Harborview Property Group'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, ''2025-06-01T00:00:00.0000000+00:00'', ''2026-06-01T00:00:00.0000000+00:00'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''POL-2025-000202'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-0000000000a3'', N''Meridian Retail Partners'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, ''2024-03-01T00:00:00.0000000+00:00'', ''2025-03-01T00:00:00.0000000+00:00'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''POL-2024-000303'', NULL, N''seed'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ClientName', N'CreatedAt', N'DeletedAt', N'EffectiveDate', N'ExpirationDate', N'IsDeleted', N'OrganizationEntityId', N'PolicyNumber', N'UpdatedAt', N'UserCreated', N'UserModified') AND [object_id] = OBJECT_ID(N'[Policies]'))
        SET IDENTITY_INSERT [Policies] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'OrganizationEntityId', N'PolicyId', N'CoverageType', N'LimitAmount', N'LimitCurrency', N'DeductibleAmount', N'DeductibleCurrency', N'CreatedAt', N'DeletedAt', N'IsDeleted', N'UpdatedAt', N'UserCreated', N'UserModified') AND [object_id] = OBJECT_ID(N'[PolicyCoverages]'))
        SET IDENTITY_INSERT [PolicyCoverages] ON;
    EXEC(N'INSERT INTO [PolicyCoverages] ([Id], [OrganizationEntityId], [PolicyId], [CoverageType], [LimitAmount], [LimitCurrency], [DeductibleAmount], [DeductibleCurrency], [CreatedAt], [DeletedAt], [IsDeleted], [UpdatedAt], [UserCreated], [UserModified])
    VALUES (''00000000-0000-0000-0000-0000000000e1'', ''11111111-1111-1111-1111-111111111111'', ''00000000-0000-0000-0000-0000000000a1'', N''Auto Liability'', 1000000.0, N''USD'', 1000.0, N''USD'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, CAST(0 AS bit), NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-0000000000e2'', ''11111111-1111-1111-1111-111111111111'', ''00000000-0000-0000-0000-0000000000a1'', N''Auto Physical Damage'', 75000.0, N''USD'', 500.0, N''USD'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, CAST(0 AS bit), NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-0000000000e3'', ''11111111-1111-1111-1111-111111111111'', ''00000000-0000-0000-0000-0000000000a2'', N''Commercial Property'', 5000000.0, N''USD'', 10000.0, N''USD'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, CAST(0 AS bit), NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-0000000000e4'', ''11111111-1111-1111-1111-111111111111'', ''00000000-0000-0000-0000-0000000000a3'', N''General Liability'', 2000000.0, N''USD'', 5000.0, N''USD'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, CAST(0 AS bit), NULL, N''seed'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'OrganizationEntityId', N'PolicyId', N'CoverageType', N'LimitAmount', N'LimitCurrency', N'DeductibleAmount', N'DeductibleCurrency', N'CreatedAt', N'DeletedAt', N'IsDeleted', N'UpdatedAt', N'UserCreated', N'UserModified') AND [object_id] = OBJECT_ID(N'[PolicyCoverages]'))
        SET IDENTITY_INSERT [PolicyCoverages] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CauseOfLossCodes_Code] ON [CauseOfLossCodes] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ClaimAuditLogs_ClaimId_CreatedAt] ON [ClaimAuditLogs] ([ClaimId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ClaimDocuments_ClaimId] ON [ClaimDocuments] ([ClaimId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ClaimParties_ClaimId] ON [ClaimParties] ([ClaimId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ClaimReserveComponents_ClaimId] ON [ClaimReserveComponents] ([ClaimId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ClaimRiskObjects_ClaimId] ON [ClaimRiskObjects] ([ClaimId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Claims_ClaimNumber] ON [Claims] ([ClaimNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Claims_PolicyId] ON [Claims] ([PolicyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ClaimStatusTransitions_FromStatus_ToStatus_RequiredPermission] ON [ClaimStatusTransitions] ([FromStatus], [ToStatus], [RequiredPermission]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_LossEvents_CauseOfLossCodeId] ON [LossEvents] ([CauseOfLossCodeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LossEvents_ClaimId] ON [LossEvents] ([ClaimId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Policies_PolicyNumber] ON [Policies] ([PolicyNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PolicyCoverages_PolicyId] ON [PolicyCoverages] ([PolicyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ReserveHistories_ReserveComponentId_ChangeSequence] ON [ReserveHistories] ([ReserveComponentId], [ChangeSequence]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260921212641_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260921212641_InitialCreate', N'9.0.20');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922171023_AddReserveHistoryRequiredTier'
)
BEGIN
    ALTER TABLE [ReserveHistories] ADD [RequiredTier] nvarchar(50) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922171023_AddReserveHistoryRequiredTier'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260922171023_AddReserveHistoryRequiredTier', N'9.0.20');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922190024_AddClaimAuditLogIdempotencyKey'
)
BEGIN
    ALTER TABLE [ClaimAuditLogs] ADD [IdempotencyKey] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922190024_AddClaimAuditLogIdempotencyKey'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ClaimAuditLogs_IdempotencyKey] ON [ClaimAuditLogs] ([IdempotencyKey]) WHERE [IdempotencyKey] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922190024_AddClaimAuditLogIdempotencyKey'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260922190024_AddClaimAuditLogIdempotencyKey', N'9.0.20');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191202_AddWithdrawnAndReopenedStatuses'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'DeletedAt', N'FromStatus', N'IsDeleted', N'OrganizationEntityId', N'RequiredPermission', N'ToStatus', N'UpdatedAt', N'UserCreated', N'UserModified') AND [object_id] = OBJECT_ID(N'[ClaimStatusTransitions]'))
        SET IDENTITY_INSERT [ClaimStatusTransitions] ON;
    EXEC(N'INSERT INTO [ClaimStatusTransitions] ([Id], [CreatedAt], [DeletedAt], [FromStatus], [IsDeleted], [OrganizationEntityId], [RequiredPermission], [ToStatus], [UpdatedAt], [UserCreated], [UserModified])
    VALUES (''00000000-0000-0000-0000-000000000125'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Draft'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Handler'', N''Withdrawn'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000126'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Draft'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Supervisor'', N''Withdrawn'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000127'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Draft'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Manager'', N''Withdrawn'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000128'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Open'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Handler'', N''Withdrawn'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000129'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Open'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Supervisor'', N''Withdrawn'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000130'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Open'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Manager'', N''Withdrawn'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000131'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''UnderInvestigation'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Handler'', N''Withdrawn'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000132'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''UnderInvestigation'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Supervisor'', N''Withdrawn'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000133'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''UnderInvestigation'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Manager'', N''Withdrawn'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000134'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Closed'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Handler'', N''Reopened'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000135'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Closed'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Supervisor'', N''Reopened'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000136'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Closed'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Manager'', N''Reopened'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000137'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Reopened'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Handler'', N''UnderInvestigation'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000138'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Reopened'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Supervisor'', N''UnderInvestigation'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000139'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Reopened'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Manager'', N''UnderInvestigation'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000140'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Reopened'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Handler'', N''PendingPayment'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000141'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Reopened'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Supervisor'', N''PendingPayment'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000142'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Reopened'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Manager'', N''PendingPayment'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000143'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Reopened'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Handler'', N''Closed'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000144'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Reopened'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Supervisor'', N''Closed'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-000000000145'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Reopened'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Manager'', N''Closed'', NULL, N''seed'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'DeletedAt', N'FromStatus', N'IsDeleted', N'OrganizationEntityId', N'RequiredPermission', N'ToStatus', N'UpdatedAt', N'UserCreated', N'UserModified') AND [object_id] = OBJECT_ID(N'[ClaimStatusTransitions]'))
        SET IDENTITY_INSERT [ClaimStatusTransitions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191202_AddWithdrawnAndReopenedStatuses'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260922191202_AddWithdrawnAndReopenedStatuses', N'9.0.20');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923072452_AddTestingSeedDataAndWorkflowPermissions'
)
BEGIN
    EXEC(N'DELETE FROM [ClaimStatusTransitions]
    WHERE [Id] = ''00000000-0000-0000-0000-000000000104'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923072452_AddTestingSeedDataAndWorkflowPermissions'
)
BEGIN
    EXEC(N'DELETE FROM [ClaimStatusTransitions]
    WHERE [Id] = ''00000000-0000-0000-0000-000000000105'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923072452_AddTestingSeedDataAndWorkflowPermissions'
)
BEGIN
    EXEC(N'DELETE FROM [ClaimStatusTransitions]
    WHERE [Id] = ''00000000-0000-0000-0000-000000000106'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923072452_AddTestingSeedDataAndWorkflowPermissions'
)
BEGIN
    EXEC(N'DELETE FROM [ClaimStatusTransitions]
    WHERE [Id] = ''00000000-0000-0000-0000-000000000134'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923072452_AddTestingSeedDataAndWorkflowPermissions'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'CreatedAt', N'DeletedAt', N'Description', N'IsActive', N'IsDeleted', N'OrganizationEntityId', N'PerilCategory', N'UpdatedAt', N'UserCreated', N'UserModified') AND [object_id] = OBJECT_ID(N'[CauseOfLossCodes]'))
        SET IDENTITY_INSERT [CauseOfLossCodes] ON;
    EXEC(N'INSERT INTO [CauseOfLossCodes] ([Id], [Code], [CreatedAt], [DeletedAt], [Description], [IsActive], [IsDeleted], [OrganizationEntityId], [PerilCategory], [UpdatedAt], [UserCreated], [UserModified])
    VALUES (''00000000-0000-0000-0000-0000000000ca'', N''LEGACY'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Legacy / retired peril'', CAST(0 AS bit), CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''Other'', NULL, N''seed'', NULL),
    (''00000000-0000-0000-0000-0000000000cb'', N''B-COLL'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, N''Collision (Org B)'', CAST(1 AS bit), CAST(0 AS bit), ''22222222-2222-2222-2222-222222222222'', N''Auto'', NULL, N''seed'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'CreatedAt', N'DeletedAt', N'Description', N'IsActive', N'IsDeleted', N'OrganizationEntityId', N'PerilCategory', N'UpdatedAt', N'UserCreated', N'UserModified') AND [object_id] = OBJECT_ID(N'[CauseOfLossCodes]'))
        SET IDENTITY_INSERT [CauseOfLossCodes] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923072452_AddTestingSeedDataAndWorkflowPermissions'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ClientName', N'CreatedAt', N'DeletedAt', N'EffectiveDate', N'ExpirationDate', N'IsDeleted', N'OrganizationEntityId', N'PolicyNumber', N'UpdatedAt', N'UserCreated', N'UserModified') AND [object_id] = OBJECT_ID(N'[Policies]'))
        SET IDENTITY_INSERT [Policies] ON;
    EXEC(N'INSERT INTO [Policies] ([Id], [ClientName], [CreatedAt], [DeletedAt], [EffectiveDate], [ExpirationDate], [IsDeleted], [OrganizationEntityId], [PolicyNumber], [UpdatedAt], [UserCreated], [UserModified])
    VALUES (''00000000-0000-0000-0000-0000000000a4'', N''Short-Term Events Co'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, ''2026-09-01T00:00:00.0000000+00:00'', ''2026-09-07T23:59:59.0000000+00:00'', CAST(0 AS bit), ''11111111-1111-1111-1111-111111111111'', N''POL-2026-000404'', NULL, N''seed'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ClientName', N'CreatedAt', N'DeletedAt', N'EffectiveDate', N'ExpirationDate', N'IsDeleted', N'OrganizationEntityId', N'PolicyNumber', N'UpdatedAt', N'UserCreated', N'UserModified') AND [object_id] = OBJECT_ID(N'[Policies]'))
        SET IDENTITY_INSERT [Policies] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923072452_AddTestingSeedDataAndWorkflowPermissions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260923072452_AddTestingSeedDataAndWorkflowPermissions', N'9.0.20');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923075703_BackfillMissingOrganizationIds'
)
BEGIN
    UPDATE rh SET rh.OrganizationEntityId = rc.OrganizationEntityId
    FROM ReserveHistories rh
    JOIN ClaimReserveComponents rc ON rc.Id = rh.ReserveComponentId
    WHERE rh.OrganizationEntityId = '00000000-0000-0000-0000-000000000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923075703_BackfillMissingOrganizationIds'
)
BEGIN
    UPDATE a SET a.OrganizationEntityId = c.OrganizationEntityId
    FROM ClaimAuditLogs a
    JOIN Claims c ON c.Id = a.ClaimId
    WHERE a.OrganizationEntityId = '00000000-0000-0000-0000-000000000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923075703_BackfillMissingOrganizationIds'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260923075703_BackfillMissingOrganizationIds', N'9.0.20');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923111544_ReserveHistoryAmountsSlaFlagAndDocumentType'
)
BEGIN
    ALTER TABLE [ReserveHistories] ADD [ChangeReason] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923111544_ReserveHistoryAmountsSlaFlagAndDocumentType'
)
BEGIN
    ALTER TABLE [ReserveHistories] ADD [NewAmount] decimal(19,4) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923111544_ReserveHistoryAmountsSlaFlagAndDocumentType'
)
BEGIN
    ALTER TABLE [ReserveHistories] ADD [NewCurrency] nvarchar(3) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923111544_ReserveHistoryAmountsSlaFlagAndDocumentType'
)
BEGIN
    ALTER TABLE [ReserveHistories] ADD [PreviousAmount] decimal(19,4) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923111544_ReserveHistoryAmountsSlaFlagAndDocumentType'
)
BEGIN
    ALTER TABLE [ReserveHistories] ADD [PreviousCurrency] nvarchar(3) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923111544_ReserveHistoryAmountsSlaFlagAndDocumentType'
)
BEGIN
    ALTER TABLE [Claims] ADD [IsSlaBreached] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923111544_ReserveHistoryAmountsSlaFlagAndDocumentType'
)
BEGIN
    ALTER TABLE [Claims] ADD [SlaBreachedAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923111544_ReserveHistoryAmountsSlaFlagAndDocumentType'
)
BEGIN
    ALTER TABLE [ClaimDocuments] ADD [DocumentType] nvarchar(50) NOT NULL DEFAULT N'Other';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923111544_ReserveHistoryAmountsSlaFlagAndDocumentType'
)
BEGIN
    UPDATE h SET
        h.PreviousAmount = ISNULL(prev.Total, 0),
        h.NewAmount = ISNULL(prev.Total, 0) + h.Amount,
        h.PreviousCurrency = h.Currency,
        h.NewCurrency = h.Currency
    FROM ReserveHistories h
    OUTER APPLY (
        SELECT SUM(e.Amount) AS Total
        FROM ReserveHistories e
        WHERE e.ReserveComponentId = h.ReserveComponentId
          AND e.ChangeSequence < h.ChangeSequence
          AND e.ApprovalStatus IN ('AutoApproved', 'Approved')
    ) prev;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923111544_ReserveHistoryAmountsSlaFlagAndDocumentType'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260923111544_ReserveHistoryAmountsSlaFlagAndDocumentType', N'9.0.20');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924204654_AddReserveComponentStatusAndApprovalStatus'
)
BEGIN
    ALTER TABLE [ClaimReserveComponents] ADD [ApprovalStatus] nvarchar(50) NOT NULL DEFAULT N'AutoApproved';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924204654_AddReserveComponentStatusAndApprovalStatus'
)
BEGIN
    ALTER TABLE [ClaimReserveComponents] ADD [Status] nvarchar(50) NOT NULL DEFAULT N'Open';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924204654_AddReserveComponentStatusAndApprovalStatus'
)
BEGIN
    UPDATE rc SET rc.ApprovalStatus = latest.ApprovalStatus
    FROM ClaimReserveComponents rc
    CROSS APPLY (
        SELECT TOP (1) h.ApprovalStatus
        FROM ReserveHistories h
        WHERE h.ReserveComponentId = rc.Id
        ORDER BY h.ChangeSequence DESC
    ) latest;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924204654_AddReserveComponentStatusAndApprovalStatus'
)
BEGIN
    UPDATE rc SET rc.Status = 'Closed'
    FROM ClaimReserveComponents rc
    JOIN Claims c ON c.Id = rc.ClaimId
    WHERE c.Status IN ('Closed', 'Withdrawn');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924204654_AddReserveComponentStatusAndApprovalStatus'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260924204654_AddReserveComponentStatusAndApprovalStatus', N'9.0.20');
END;

COMMIT;
GO

