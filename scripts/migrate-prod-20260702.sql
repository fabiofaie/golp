-- Migration: 20260702120304_AddSinglesSupport
-- Idempotente: ogni blocco controlla __EFMigrationsHistory prima di eseguire.

BEGIN TRANSACTION;

-- 1. Aggiungi colonna AllowsSingles a Sports
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702120304_AddSinglesSupport'
)
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'[Sports]') AND name = N'AllowsSingles'
    )
    BEGIN
        ALTER TABLE [Sports] ADD [AllowsSingles] bit NOT NULL DEFAULT CAST(0 AS bit);
    END
END;

-- 2. Rendi nullable Team1Player2Id su Matches
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702120304_AddSinglesSupport'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Matches]') AND [c].[name] = N'Team1Player2Id');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Matches] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [Matches] ALTER COLUMN [Team1Player2Id] uniqueidentifier NULL;
END;

-- 3. Rendi nullable Team2Player2Id su Matches
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702120304_AddSinglesSupport'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Matches]') AND [c].[name] = N'Team2Player2Id');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Matches] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [Matches] ALTER COLUMN [Team2Player2Id] uniqueidentifier NULL;
END;

-- 4. Aggiungi colonna IsSingles a Matches
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702120304_AddSinglesSupport'
)
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'[Matches]') AND name = N'IsSingles'
    )
    BEGIN
        ALTER TABLE [Matches] ADD [IsSingles] bit NOT NULL DEFAULT CAST(0 AS bit);
    END
END;

-- 5. Seed AllowsSingles su Sports (padel=1 e beachtennis=2 → true; basket=3 e burraco=4 → false)
-- Dynamic SQL: la colonna potrebbe non esistere ancora a compile time
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702120304_AddSinglesSupport'
)
BEGIN
    EXEC(N'UPDATE [Sports] SET [AllowsSingles] = CAST(1 AS bit) WHERE [Id] = 1');
    EXEC(N'UPDATE [Sports] SET [AllowsSingles] = CAST(1 AS bit) WHERE [Id] = 2');
    EXEC(N'UPDATE [Sports] SET [AllowsSingles] = CAST(0 AS bit) WHERE [Id] = 3');
    EXEC(N'UPDATE [Sports] SET [AllowsSingles] = CAST(0 AS bit) WHERE [Id] = 4');
END;

-- 6. Registra migration in __EFMigrationsHistory
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702120304_AddSinglesSupport'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260702120304_AddSinglesSupport', N'10.0.9');
END;

COMMIT;
GO
