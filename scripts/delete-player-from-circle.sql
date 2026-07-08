-- ============================================================
-- CANCELLA TUTTE LE PARTITE DI UN GIOCATORE IN UN CIRCOLO
-- E LA SUA APPARTENENZA AL CIRCOLO
-- Sostituisci @UserName e @CircleId con i valori reali.
-- ============================================================

DECLARE @UserName NVARCHAR(200) = 'NOME_UTENTE'; -- << CAMBIA QUI
DECLARE @CircleId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000'; -- << CAMBIA QUI

BEGIN TRANSACTION;

BEGIN TRY

    DECLARE @UserId UNIQUEIDENTIFIER;

    SELECT @UserId = Id FROM Users WHERE Name = @UserName;

    IF @UserId IS NULL
        THROW 50001, 'Utente non trovato.', 1;

    IF NOT EXISTS (SELECT 1 FROM CircleMemberships WHERE CircleId = @CircleId AND UserId = @UserId)
        THROW 50002, 'Utente non e'' membro di questo circolo.', 1;

    -- Preview partite da eliminare
    SELECT Id, Status, CreatedAt
    FROM Matches
    WHERE CircleId = @CircleId
      AND @UserId IN (Team1Player1Id, Team1Player2Id, Team2Player1Id, Team2Player2Id);

    -- figli di Match (conferme e set) per le partite del giocatore in questo circolo
    DELETE mc
    FROM MatchConfirmations mc
    INNER JOIN Matches m ON m.Id = mc.MatchId
    WHERE m.CircleId = @CircleId
      AND @UserId IN (m.Team1Player1Id, m.Team1Player2Id, m.Team2Player1Id, m.Team2Player2Id);

    DELETE ms
    FROM MatchSets ms
    INNER JOIN Matches m ON m.Id = ms.MatchId
    WHERE m.CircleId = @CircleId
      AND @UserId IN (m.Team1Player1Id, m.Team1Player2Id, m.Team2Player1Id, m.Team2Player2Id);

    -- partite del giocatore in questo circolo
    DELETE FROM Matches
    WHERE CircleId = @CircleId
      AND @UserId IN (Team1Player1Id, Team1Player2Id, Team2Player1Id, Team2Player2Id);

    -- appartenenza al circolo
    DELETE FROM CircleMemberships
    WHERE CircleId = @CircleId AND UserId = @UserId;

    COMMIT TRANSACTION;

    SELECT 'OK — partite e membership eliminate' AS Risultato;

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    SELECT
        ERROR_NUMBER()  AS ErrorNumber,
        ERROR_MESSAGE() AS ErrorMessage;
END CATCH;
