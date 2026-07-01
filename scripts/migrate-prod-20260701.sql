BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260629130355_AddAwardNotificationsSent'
)
BEGIN
    CREATE TABLE [AwardNotificationsSent] (
        [Id] uniqueidentifier NOT NULL,
        [CircleId] uniqueidentifier NOT NULL,
        [PeriodType] nvarchar(10) NOT NULL,
        [PeriodLabel] nvarchar(20) NOT NULL,
        [ProcessedAt] datetimeoffset NOT NULL,
        [EmailSent] bit NOT NULL,
        CONSTRAINT [PK_AwardNotificationsSent] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260629130355_AddAwardNotificationsSent'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AwardNotificationsSent_CircleId_PeriodType_PeriodLabel] ON [AwardNotificationsSent] ([CircleId], [PeriodType], [PeriodLabel]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260629130355_AddAwardNotificationsSent'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260629130355_AddAwardNotificationsSent', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260630145944_AddGuestUserFields'
)
BEGIN
    DROP INDEX [IX_Users_Email] ON [Users];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260630145944_AddGuestUserFields'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'Email');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [Users] ALTER COLUMN [Email] nvarchar(254) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260630145944_AddGuestUserFields'
)
BEGIN
    ALTER TABLE [Users] ADD [IsActivated] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260630145944_AddGuestUserFields'
)
BEGIN
    EXEC(N'UPDATE [Users] SET [IsActivated] = 1 WHERE [PasswordHash] != ''''')
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260630145944_AddGuestUserFields'
)
BEGIN
    ALTER TABLE [Users] ADD [Phone] nvarchar(30) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260630145944_AddGuestUserFields'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]) WHERE [Email] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260630145944_AddGuestUserFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260630145944_AddGuestUserFields', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260630154843_AddMatchConfirmationTokens'
)
BEGIN
    CREATE TABLE [MatchConfirmationTokens] (
        [Id] uniqueidentifier NOT NULL,
        [MatchId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Token] uniqueidentifier NOT NULL,
        [ExpiresAt] datetimeoffset NOT NULL,
        [UsedAt] datetimeoffset NULL,
        CONSTRAINT [PK_MatchConfirmationTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MatchConfirmationTokens_Matches_MatchId] FOREIGN KEY ([MatchId]) REFERENCES [Matches] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_MatchConfirmationTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260630154843_AddMatchConfirmationTokens'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MatchConfirmationTokens_MatchId_UserId] ON [MatchConfirmationTokens] ([MatchId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260630154843_AddMatchConfirmationTokens'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MatchConfirmationTokens_Token] ON [MatchConfirmationTokens] ([Token]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260630154843_AddMatchConfirmationTokens'
)
BEGIN
    CREATE INDEX [IX_MatchConfirmationTokens_UserId] ON [MatchConfirmationTokens] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260630154843_AddMatchConfirmationTokens'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260630154843_AddMatchConfirmationTokens', N'10.0.9');
END;

COMMIT;
GO

