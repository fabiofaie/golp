-- ============================================================================
-- Rettifica partita inserita con punteggio INVERTITO (team1 <-> team2).
-- Metodo: negazione delta (approssimato). Valido solo se NON esistono partite
-- confermate successive per gli stessi giocatori (caso "ultima partita").
--
-- Effetto: scambia i set, inverte WinnerTeam, nega i delta ELO memorizzati e
-- storna i rating dei 4 giocatori del corrispondente importo (rating -= 2*delta
-- perché lo slot del giocatore non cambia, cambia solo il segno del suo delta).
--
-- NB: la magnitudo dei delta NON viene ricalcolata. Per un valore esatto serve
-- il ricalcolo via RatingService (vedi conversazione). Qui solo segno invertito.
-- ============================================================================

SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @MatchId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000'; -- <<< METTI QUI L'ID PARTITA

DECLARE @CircleId UNIQUEIDENTIFIER,
        @Status NVARCHAR(50),
        @WinnerTeam INT,
        @t1p1 UNIQUEIDENTIFIER, @t1p2 UNIQUEIDENTIFIER,
        @t2p1 UNIQUEIDENTIFIER, @t2p2 UNIQUEIDENTIFIER,
        @d1 INT, @d2 INT, @d3 INT, @d4 INT;

SELECT @CircleId = CircleId, @Status = Status, @WinnerTeam = WinnerTeam,
       @t1p1 = Team1Player1Id, @t1p2 = Team1Player2Id,
       @t2p1 = Team2Player1Id, @t2p2 = Team2Player2Id,
       @d1 = DeltaTeam1Player1, @d2 = DeltaTeam1Player2,
       @d3 = DeltaTeam2Player1, @d4 = DeltaTeam2Player2
FROM Matches
WHERE Id = @MatchId;

IF @CircleId IS NULL
BEGIN
    RAISERROR('Partita non trovata: %s', 16, 1);
    ROLLBACK TRAN; RETURN;
END

IF @Status <> 'confirmed' OR @d1 IS NULL
BEGIN
    RAISERROR('Partita non confermata o senza delta applicati (Status=%s). Nessun rating da stornare.', 16, 1, @Status);
    ROLLBACK TRAN; RETURN;
END

-- 1. Storna i rating: ogni giocatore torna indietro di 2*delta (delta vecchio rimosso + delta negato applicato)
UPDATE CircleMemberships SET Rating = Rating - 2 * @d1 WHERE CircleId = @CircleId AND UserId = @t1p1;
UPDATE CircleMemberships SET Rating = Rating - 2 * @d2 WHERE CircleId = @CircleId AND UserId = @t1p2;
UPDATE CircleMemberships SET Rating = Rating - 2 * @d3 WHERE CircleId = @CircleId AND UserId = @t2p1;
UPDATE CircleMemberships SET Rating = Rating - 2 * @d4 WHERE CircleId = @CircleId AND UserId = @t2p2;

-- 2. Inverti WinnerTeam e nega i delta memorizzati
UPDATE Matches
SET WinnerTeam       = 3 - WinnerTeam,
    DeltaTeam1Player1 = -DeltaTeam1Player1,
    DeltaTeam1Player2 = -DeltaTeam1Player2,
    DeltaTeam2Player1 = -DeltaTeam2Player1,
    DeltaTeam2Player2 = -DeltaTeam2Player2
WHERE Id = @MatchId;

-- 3. Scambia i punteggi di ogni set
UPDATE MatchSets
SET Team1Score = Team2Score,
    Team2Score = Team1Score
WHERE MatchId = @MatchId;

-- Verifica prima di committare
SELECT 'Match' AS Tabella, Id, WinnerTeam, DeltaTeam1Player1, DeltaTeam1Player2, DeltaTeam2Player1, DeltaTeam2Player2
FROM Matches WHERE Id = @MatchId;
SELECT 'Sets' AS Tabella, SetNumber, Team1Score, Team2Score FROM MatchSets WHERE MatchId = @MatchId ORDER BY SetNumber;
SELECT 'Ratings' AS Tabella, UserId, Rating FROM CircleMemberships
WHERE CircleId = @CircleId AND UserId IN (@t1p1, @t1p2, @t2p1, @t2p2);

-- Controlla i risultati sopra, poi:
COMMIT TRAN;     -- esegui per confermare
-- ROLLBACK TRAN; -- oppure annulla
