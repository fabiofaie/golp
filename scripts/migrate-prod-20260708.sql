-- Migration: 20260708092749_AddGameBonusRatingMethod
-- Idempotente: ogni blocco controlla __EFMigrationsHistory prima di eseguire.

BEGIN TRANSACTION;

-- 1. Aggiungi colonna GameBonusWinnerPoints a Matches
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260708092749_AddGameBonusRatingMethod'
)
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'[Matches]') AND name = N'GameBonusWinnerPoints'
    )
    BEGIN
        ALTER TABLE [Matches] ADD [GameBonusWinnerPoints] int NULL;
    END
END;

-- 2. Aggiungi colonna GameBonusWindowMatches a Circles
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260708092749_AddGameBonusRatingMethod'
)
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'[Circles]') AND name = N'GameBonusWindowMatches'
    )
    BEGIN
        ALTER TABLE [Circles] ADD [GameBonusWindowMatches] int NOT NULL DEFAULT 30;
    END
END;

-- 3. Aggiungi colonna GameBonusWindowWeeks a Circles
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260708092749_AddGameBonusRatingMethod'
)
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'[Circles]') AND name = N'GameBonusWindowWeeks'
    )
    BEGIN
        ALTER TABLE [Circles] ADD [GameBonusWindowWeeks] int NOT NULL DEFAULT 6;
    END
END;

-- 4. Aggiungi colonna RatingMethod a Circles
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260708092749_AddGameBonusRatingMethod'
)
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'[Circles]') AND name = N'RatingMethod'
    )
    BEGIN
        ALTER TABLE [Circles] ADD [RatingMethod] nvarchar(20) NOT NULL DEFAULT N'Elo';
    END
END;

-- 5. Registra migration in __EFMigrationsHistory
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260708092749_AddGameBonusRatingMethod'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260708092749_AddGameBonusRatingMethod', N'10.0.9');
END;

COMMIT;
GO
