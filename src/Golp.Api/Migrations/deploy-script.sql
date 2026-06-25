BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260624145528_AddSportsTable'
)
BEGIN
    CREATE TABLE [Sports] (
        [Id] int NOT NULL IDENTITY,
        [Key] nvarchar(50) NOT NULL,
        [DisplayName] nvarchar(100) NOT NULL,
        [PointUnit] nvarchar(20) NOT NULL,
        [Sets] bit NOT NULL,
        [TeamSize] int NOT NULL,
        [IsActive] bit NOT NULL,
        [SetWeight] float NOT NULL,
        CONSTRAINT [PK_Sports] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260624145528_AddSportsTable'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'DisplayName', N'IsActive', N'Key', N'PointUnit', N'SetWeight', N'Sets', N'TeamSize') AND [object_id] = OBJECT_ID(N'[Sports]'))
        SET IDENTITY_INSERT [Sports] ON;
    EXEC(N'INSERT INTO [Sports] ([Id], [DisplayName], [IsActive], [Key], [PointUnit], [SetWeight], [Sets], [TeamSize])
    VALUES (1, N''Padel'', CAST(1 AS bit), N''padel'', N''games'', 0.40000000000000002E0, CAST(1 AS bit), 2),
    (2, N''Beach Tennis'', CAST(1 AS bit), N''beachtennis'', N''games'', 0.40000000000000002E0, CAST(1 AS bit), 2),
    (3, N''Basket 2v2'', CAST(1 AS bit), N''basket2v2'', N''points'', 0.0E0, CAST(0 AS bit), 2),
    (4, N''Burraco'', CAST(1 AS bit), N''burraco'', N''score'', 0.0E0, CAST(0 AS bit), 2)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'DisplayName', N'IsActive', N'Key', N'PointUnit', N'SetWeight', N'Sets', N'TeamSize') AND [object_id] = OBJECT_ID(N'[Sports]'))
        SET IDENTITY_INSERT [Sports] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260624145528_AddSportsTable'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Sports_Key] ON [Sports] ([Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260624145528_AddSportsTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260624145528_AddSportsTable', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260625142144_AddUserSecurityStamp'
)
BEGIN
    ALTER TABLE [Users] ADD [SecurityStamp] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260625142144_AddUserSecurityStamp'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260625142144_AddUserSecurityStamp', N'10.0.9');
END;

COMMIT;
GO

