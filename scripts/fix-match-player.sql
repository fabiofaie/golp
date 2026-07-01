-- ============================================================
-- fix-match-player.sql
-- Sostituisce un giocatore errato in una partita confermata.
-- Annulla il delta ELO dal giocatore errato e lo applica
-- al giocatore corretto.
--
-- ISTRUZIONI:
--   1. Imposta i tre parametri nella sezione PARAMETRI.
--   2. Esegui in una transazione e verifica il risultato
--      prima di fare COMMIT.
-- ============================================================

BEGIN TRANSACTION;

-- ============================================================
-- PARAMETRI — modifica questi tre valori
-- ============================================================
DECLARE @MatchId       UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';
DECLARE @WrongPlayerId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';
DECLARE @RightPlayerId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';
-- ============================================================

-- Recupera dati partita
DECLARE @CircleId      UNIQUEIDENTIFIER;
DECLARE @Status        NVARCHAR(20);
DECLARE @Slot          NVARCHAR(30);
DECLARE @Delta         INT;

SELECT
    @CircleId = CircleId,
    @Status   = Status,
    @Slot     = CASE
                    WHEN Team1Player1Id = @WrongPlayerId THEN 'Team1Player1'
                    WHEN Team1Player2Id = @WrongPlayerId THEN 'Team1Player2'
                    WHEN Team2Player1Id = @WrongPlayerId THEN 'Team2Player1'
                    WHEN Team2Player2Id = @WrongPlayerId THEN 'Team2Player2'
                    ELSE NULL
                END,
    @Delta    = CASE
                    WHEN Team1Player1Id = @WrongPlayerId THEN DeltaTeam1Player1
                    WHEN Team1Player2Id = @WrongPlayerId THEN DeltaTeam1Player2
                    WHEN Team2Player1Id = @WrongPlayerId THEN DeltaTeam2Player1
                    WHEN Team2Player2Id = @WrongPlayerId THEN DeltaTeam2Player2
                    ELSE NULL
                END
FROM Matches
WHERE Id = @MatchId;

-- Guardrail: partita deve esistere e il giocatore deve essere presente
IF @Slot IS NULL
BEGIN
    RAISERROR('Giocatore errato non trovato nella partita specificata.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END

-- Guardrail: avviso se partita non confermata (delta potrebbe essere NULL)
IF @Status <> 'confirmed'
    PRINT 'ATTENZIONE: la partita non è confermata — i delta ELO potrebbero essere NULL.';

-- Guardrail: giocatore corretto deve essere membro del circolo
IF NOT EXISTS (
    SELECT 1 FROM CircleMemberships
    WHERE CircleId = @CircleId AND UserId = @RightPlayerId
)
BEGIN
    RAISERROR('Il giocatore corretto non è membro del circolo.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END

-- Stampa riepilogo operazione
PRINT CONCAT('Match: ', CAST(@MatchId AS NVARCHAR(36)));
PRINT CONCAT('Circolo: ', CAST(@CircleId AS NVARCHAR(36)));
PRINT CONCAT('Slot: ', @Slot);
PRINT CONCAT('Delta ELO: ', CAST(ISNULL(@Delta, 0) AS NVARCHAR(10)));

-- 1. Aggiorna slot giocatore nella partita
UPDATE Matches
SET
    Team1Player1Id = CASE WHEN @Slot = 'Team1Player1' THEN @RightPlayerId ELSE Team1Player1Id END,
    Team1Player2Id = CASE WHEN @Slot = 'Team1Player2' THEN @RightPlayerId ELSE Team1Player2Id END,
    Team2Player1Id = CASE WHEN @Slot = 'Team2Player1' THEN @RightPlayerId ELSE Team2Player1Id END,
    Team2Player2Id = CASE WHEN @Slot = 'Team2Player2' THEN @RightPlayerId ELSE Team2Player2Id END
WHERE Id = @MatchId;

-- 2. Sostituisci in MatchConfirmations (se il giocatore errato aveva confermato)
UPDATE MatchConfirmations
SET UserId = @RightPlayerId
WHERE MatchId = @MatchId AND UserId = @WrongPlayerId;

-- 3. Annulla delta dal giocatore errato (solo se delta non NULL)
IF @Delta IS NOT NULL
BEGIN
    UPDATE CircleMemberships
    SET Rating = Rating - @Delta
    WHERE CircleId = @CircleId AND UserId = @WrongPlayerId;

    -- 4. Applica delta al giocatore corretto
    UPDATE CircleMemberships
    SET Rating = Rating + @Delta
    WHERE CircleId = @CircleId AND UserId = @RightPlayerId;
END
ELSE
    PRINT 'Delta NULL — rating non modificato (partita non confermata o delta non calcolato).';

-- Verifica finale
SELECT
    m.Id           AS MatchId,
    m.Status,
    m.Team1Player1Id, m.DeltaTeam1Player1,
    m.Team1Player2Id, m.DeltaTeam1Player2,
    m.Team2Player1Id, m.DeltaTeam2Player1,
    m.Team2Player2Id, m.DeltaTeam2Player2,
    wrong.Rating   AS WrongPlayerRating,
    right_.Rating  AS RightPlayerRating
FROM Matches m
LEFT JOIN CircleMemberships wrong  ON wrong.CircleId  = m.CircleId AND wrong.UserId  = @WrongPlayerId
LEFT JOIN CircleMemberships right_ ON right_.CircleId = m.CircleId AND right_.UserId = @RightPlayerId
WHERE m.Id = @MatchId;

-- ============================================================
-- Controlla il risultato sopra, poi:
--   COMMIT TRANSACTION;   ← tutto ok
--   ROLLBACK TRANSACTION; ← annulla tutto
-- ============================================================
ROLLBACK TRANSACTION; -- rimosso quando sei sicuro
