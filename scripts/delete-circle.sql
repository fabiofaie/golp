-- Cancella un circle con tutte le sue dipendenze.
-- Sostituisci il GUID con l'Id del circle da eliminare.
DECLARE @CircleId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';

BEGIN TRANSACTION;

-- figli di Match
DELETE mc
FROM MatchConfirmations mc
INNER JOIN Matches m ON m.Id = mc.MatchId
WHERE m.CircleId = @CircleId;

DELETE ms
FROM MatchSets ms
INNER JOIN Matches m ON m.Id = ms.MatchId
WHERE m.CircleId = @CircleId;

DELETE FROM Matches           WHERE CircleId = @CircleId;
DELETE FROM CircleAwards      WHERE CircleId = @CircleId;
DELETE FROM CircleMemberships WHERE CircleId = @CircleId;
DELETE FROM Circles           WHERE Id = @CircleId;

COMMIT;
