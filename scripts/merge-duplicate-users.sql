-- ============================================================================
-- Merge utenti duplicati in un unico utente "vero".
-- Problema: stesso giocatore registrato N volte con id diversi.
-- Soluzione: riassegna partite/rating/conferme al vero utente, cancella duplicati.
--
-- ATTENZIONE: esegui in una finestra di query separata con ROLLBACK prima,
-- verifica i SELECT di controllo, poi sostituisci ROLLBACK con COMMIT.
--
-- Impatto su CircleMemberships: se il dup è nella stessa circle del reale,
-- i rating del dup vengono sommati al reale (storno approssimato). Se invece
-- il dup è in una circle dove il reale non è presente, la membership viene
-- semplicemente riassegnata al reale.
-- ============================================================================

SET XACT_ABORT ON;
BEGIN TRAN;

-- <<< CONFIGURA QUI >>>
DECLARE @RealUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';

-- Trova automaticamente tutti i duplicati (stesso Name, id diverso)
DECLARE @RealUserName NVARCHAR(200);
SELECT @RealUserName = Name FROM Users WHERE Id = @RealUserId;

IF @RealUserName IS NULL
BEGIN
    RAISERROR('Utente reale non trovato: %s', 16, 1, 'RealUserId non valido');
    ROLLBACK TRAN; RETURN;
END

-- Tabella temporanea con gli id da eliminare
CREATE TABLE #DupIds (UserId UNIQUEIDENTIFIER);
INSERT INTO #DupIds (UserId)
SELECT Id FROM Users
WHERE Name = @RealUserName AND Id <> @RealUserId;

DECLARE @DupCount INT = (SELECT COUNT(*) FROM #DupIds);
IF @DupCount = 0
BEGIN
    RAISERROR('Nessun duplicato trovato per il nome "%s".', 16, 1, @RealUserName);
    ROLLBACK TRAN; RETURN;
END

PRINT CONCAT('Trovati ', @DupCount, ' duplicati di "', @RealUserName, '". Avvio merge...');

-- -------------------------------------------------------------------------
-- 1. CircleMemberships
--    Caso A: il reale è già nella stessa circle del dup → somma rating, elimina dup
--    Caso B: il reale NON è nella circle del dup → riassegna UserId al reale
-- -------------------------------------------------------------------------

-- Caso A: somma rating di TUTTI i dup al reale (nelle circle condivise)
-- Subquery aggrega prima per evitare che il JOIN multi-riga applichi un solo delta
UPDATE real_cm
SET real_cm.Rating = real_cm.Rating + agg.TotalDelta
FROM CircleMemberships real_cm
INNER JOIN (
    SELECT CircleId,
           SUM(Rating) - COUNT(*) * 1000 AS TotalDelta  -- delta netto: togli seed 1000 per ogni dup
    FROM CircleMemberships
    WHERE UserId IN (SELECT UserId FROM #DupIds)
    GROUP BY CircleId
) agg ON agg.CircleId = real_cm.CircleId
WHERE real_cm.UserId = @RealUserId;

-- Caso A: elimina le membership del dup nelle circle condivise
DELETE dup_cm
FROM CircleMemberships dup_cm
INNER JOIN CircleMemberships real_cm
    ON real_cm.CircleId = dup_cm.CircleId
    AND real_cm.UserId = @RealUserId
WHERE dup_cm.UserId IN (SELECT UserId FROM #DupIds);

-- Caso B: riassegna le membership nelle circle dove il reale non è presente
UPDATE CircleMemberships
SET UserId = @RealUserId
WHERE UserId IN (SELECT UserId FROM #DupIds);

-- -------------------------------------------------------------------------
-- 2. Matches — tutti i campi player e creatore
-- -------------------------------------------------------------------------
UPDATE Matches SET Team1Player1Id = @RealUserId WHERE Team1Player1Id IN (SELECT UserId FROM #DupIds);
UPDATE Matches SET Team1Player2Id = @RealUserId WHERE Team1Player2Id IN (SELECT UserId FROM #DupIds);
UPDATE Matches SET Team2Player1Id = @RealUserId WHERE Team2Player1Id IN (SELECT UserId FROM #DupIds);
UPDATE Matches SET Team2Player2Id = @RealUserId WHERE Team2Player2Id IN (SELECT UserId FROM #DupIds);
UPDATE Matches SET CreatedById    = @RealUserId WHERE CreatedById    IN (SELECT UserId FROM #DupIds);

-- -------------------------------------------------------------------------
-- 3. MatchConfirmations
--    Se real+dup hanno già confermato lo stesso match → elimina la dup (evita unique violation)
-- -------------------------------------------------------------------------
DELETE dup_mc
FROM MatchConfirmations dup_mc
INNER JOIN MatchConfirmations real_mc
    ON real_mc.MatchId = dup_mc.MatchId
    AND real_mc.UserId = @RealUserId
WHERE dup_mc.UserId IN (SELECT UserId FROM #DupIds);

-- Riassegna le restanti
UPDATE MatchConfirmations
SET UserId = @RealUserId
WHERE UserId IN (SELECT UserId FROM #DupIds);

-- -------------------------------------------------------------------------
-- 4. CircleAwards
-- -------------------------------------------------------------------------
UPDATE CircleAwards
SET WinnerUserId = @RealUserId
WHERE WinnerUserId IN (SELECT UserId FROM #DupIds);

-- -------------------------------------------------------------------------
-- 5. FcmTokens — riassegna notifiche push al reale
-- -------------------------------------------------------------------------
UPDATE FcmTokens
SET UserId = @RealUserId
WHERE UserId IN (SELECT UserId FROM #DupIds);

-- -------------------------------------------------------------------------
-- 6. Pulizia token di sessione/reset (non hanno valore, si rigenereranno)
-- -------------------------------------------------------------------------
DELETE FROM PasswordResetTokens WHERE UserId IN (SELECT UserId FROM #DupIds);
DELETE FROM RefreshTokens        WHERE UserId IN (SELECT UserId FROM #DupIds);

-- -------------------------------------------------------------------------
-- 7. Elimina gli utenti duplicati
-- -------------------------------------------------------------------------
DELETE FROM Users WHERE Id IN (SELECT UserId FROM #DupIds);

-- -------------------------------------------------------------------------
-- VERIFICA — controlla prima di committare
-- -------------------------------------------------------------------------
SELECT 'Utente reale' AS Info, Id, Name, Email, CreatedAt FROM Users WHERE Id = @RealUserId;

SELECT
    'Ranking post-merge' AS Info,
    c.Name               AS CircleName,
    cm.Rating            AS NuovoRating,
    cm.JoinedAt
FROM CircleMemberships cm
JOIN Circles c ON c.Id = cm.CircleId
WHERE cm.UserId = @RealUserId
ORDER BY c.Name;

SELECT 'Partite come giocatore' AS Info, COUNT(*) AS Totale
FROM Matches
WHERE Team1Player1Id = @RealUserId OR Team1Player2Id = @RealUserId
   OR Team2Player1Id = @RealUserId OR Team2Player2Id = @RealUserId;

SELECT 'Duplicati rimasti' AS Info, COUNT(*) AS Totale
FROM Users WHERE Name = @RealUserName AND Id <> @RealUserId;

DROP TABLE #DupIds;

-- Controlla i risultati sopra, poi scegli:
ROLLBACK TRAN;   -- annulla (test)
-- COMMIT TRAN;  -- conferma (produzione)
