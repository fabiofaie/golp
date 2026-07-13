BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712062901_AddCircleAttendance'
)
BEGIN
    CREATE TABLE [CircleAttendances] (
        [Id] uniqueidentifier NOT NULL,
        [CircleId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_CircleAttendances] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CircleAttendances_Circles_CircleId] FOREIGN KEY ([CircleId]) REFERENCES [Circles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CircleAttendances_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712062901_AddCircleAttendance'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CircleAttendances_CircleId_UserId] ON [CircleAttendances] ([CircleId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712062901_AddCircleAttendance'
)
BEGIN
    CREATE INDEX [IX_CircleAttendances_UserId] ON [CircleAttendances] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712062901_AddCircleAttendance'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260712062901_AddCircleAttendance', N'10.0.9');
END;

COMMIT;
GO


