-- ============================================================
-- ANNULLA PARTITA CONFERMATA E RIPRISTINA ELO
-- Sostituisci @MatchId con l'ID reale della partita
-- ============================================================

DECLARE @MatchId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000'; -- << CAMBIA QUI

BEGIN TRANSACTION;

BEGIN TRY

    -- 1. Verifica che la partita esista e sia confermata
    DECLARE @Status      NVARCHAR(50);
    DECLARE @CircleId    UNIQUEIDENTIFIER;
    DECLARE @P1          UNIQUEIDENTIFIER;
    DECLARE @P2          UNIQUEIDENTIFIER;
    DECLARE @P3          UNIQUEIDENTIFIER;
    DECLARE @P4          UNIQUEIDENTIFIER;
    DECLARE @D1          INT;
    DECLARE @D2          INT;
    DECLARE @D3          INT;
    DECLARE @D4          INT;

    SELECT
        @Status   = Status,
        @CircleId = CircleId,
        @P1       = Team1Player1Id,
        @P2       = Team1Player2Id,
        @P3       = Team2Player1Id,
        @P4       = Team2Player2Id,
        @D1       = DeltaTeam1Player1,
        @D2       = DeltaTeam1Player2,
        @D3       = DeltaTeam2Player1,
        @D4       = DeltaTeam2Player2
    FROM Matches
    WHERE Id = @MatchId;

    IF @Status IS NULL
        THROW 50001, 'Partita non trovata.', 1;

    IF @Status <> 'confirmed'
        THROW 50002, 'Partita non e'' confirmed — nessun ELO da ripristinare.', 1;

    IF @D1 IS NULL
        THROW 50003, 'Delta ELO nulli — partita confermata senza calcolo ELO?', 1;

    -- 2. Preview valori prima della modifica (per log)
    SELECT
        u.Name                    AS Giocatore,
        cm.Rating                 AS RatingAttuale,
        cm.Rating - CASE cm.UserId
            WHEN @P1 THEN @D1
            WHEN @P2 THEN @D2
            WHEN @P3 THEN @D3
            WHEN @P4 THEN @D4
            ELSE 0
        END                       AS RatingDopo
    FROM CircleMemberships cm
    JOIN Users u ON u.Id = cm.UserId
    WHERE cm.CircleId = @CircleId
      AND cm.UserId IN (@P1, @P2, @P3, @P4);

    -- 3. Ripristina ELO: sottrai il delta guadagnato (o re-aggiungi quello perso)
    UPDATE CircleMemberships
    SET Rating = Rating - @D1
    WHERE CircleId = @CircleId AND UserId = @P1;

    UPDATE CircleMemberships
    SET Rating = Rating - @D2
    WHERE CircleId = @CircleId AND UserId = @P2 AND @P2 IS NOT NULL;

    UPDATE CircleMemberships
    SET Rating = Rating - @D3
    WHERE CircleId = @CircleId AND UserId = @P3;

    UPDATE CircleMemberships
    SET Rating = Rating - @D4
    WHERE CircleId = @CircleId AND UserId = @P4 AND @P4 IS NOT NULL;

    -- 4. Elimina la partita (CASCADE: MatchSets, MatchConfirmations, MatchConfirmationTokens)
    DELETE FROM Matches WHERE Id = @MatchId;

    COMMIT TRANSACTION;

    -- 5. Riepilogo finale
    SELECT 'OK — partita eliminata dal DB, ELO ripristinato' AS Risultato;

    SELECT
        u.Name    AS Giocatore,
        cm.Rating AS NuovoRating
    FROM CircleMemberships cm
    JOIN Users u ON u.Id = cm.UserId
    WHERE cm.CircleId = @CircleId
      AND cm.UserId IN (@P1, @P2, @P3, @P4);

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    SELECT
        ERROR_NUMBER()  AS ErrorNumber,
        ERROR_MESSAGE() AS ErrorMessage;
END CATCH;
