-- ============================================================
-- find-matches-by-players.sql
-- Cerca tutte le partite che coinvolgono 4 giocatori dati.
-- Usa LIKE per matching parziale sui nomi.
--
-- ISTRUZIONI:
--   1. Imposta i 4 nomi nella sezione PARAMETRI (% = wildcard).
--   2. Esegui in sola lettura — nessuna modifica al DB.
-- ============================================================

-- ============================================================
-- PARAMETRI — modifica questi valori
-- ============================================================
DECLARE @Name1 NVARCHAR(200) = '%mario%';
DECLARE @Name2 NVARCHAR(200) = '%luigi%';
DECLARE @Name3 NVARCHAR(200) = '%anna%';
DECLARE @Name4 NVARCHAR(200) = '%sara%';
-- ============================================================

-- Risolvi gli ID dei 4 giocatori (mostra i candidati trovati)
SELECT 'Candidati trovati' AS Info, u.Id, u.Name, u.Email
FROM Users u
WHERE u.Name LIKE @Name1
   OR u.Name LIKE @Name2
   OR u.Name LIKE @Name3
   OR u.Name LIKE @Name4;

-- Raccoglie gli Id dei giocatori che matchano i 4 nomi
;WITH Players AS (
    SELECT u.Id, u.Name
    FROM Users u
    WHERE u.Name LIKE @Name1
       OR u.Name LIKE @Name2
       OR u.Name LIKE @Name3
       OR u.Name LIKE @Name4
),
-- Partite in cui tutti e 4 gli slot appartengono ai giocatori trovati
MatchingMatches AS (
    SELECT DISTINCT m.Id AS MatchId
    FROM Matches m
    WHERE m.Team1Player1Id IN (SELECT Id FROM Players)
      AND m.Team2Player1Id IN (SELECT Id FROM Players)
      AND (m.Team1Player2Id IS NULL OR m.Team1Player2Id IN (SELECT Id FROM Players))
      AND (m.Team2Player2Id IS NULL OR m.Team2Player2Id IN (SELECT Id FROM Players))
)
SELECT
    m.Id                                        AS MatchId,
    c.Name                                      AS Circolo,
    m.Status,
    m.WinnerTeam,
    m.CreatedAt,

    -- Team 1
    u1.Name                                     AS Team1P1,
    m.DeltaTeam1Player1                         AS DeltaT1P1,
    u2.Name                                     AS Team1P2,
    m.DeltaTeam1Player2                         AS DeltaT1P2,
    (SELECT STRING_AGG(CONCAT(ms.SetNumber,': ',ms.Team1Score,'-',ms.Team2Score), ' | ')
     FROM MatchSets ms WHERE ms.MatchId = m.Id) AS Punteggi,

    -- Team 2
    u3.Name                                     AS Team2P1,
    m.DeltaTeam2Player1                         AS DeltaT2P1,
    u4.Name                                     AS Team2P2,
    m.DeltaTeam2Player2                         AS DeltaT2P2,

    -- Chi ha registrato
    ub.Name                                     AS RegistrataDa,
    m.ForceConfirmedAt
FROM Matches m
JOIN MatchingMatches mm   ON mm.MatchId       = m.Id
JOIN Circles c            ON c.Id             = m.CircleId
LEFT JOIN Users u1        ON u1.Id            = m.Team1Player1Id
LEFT JOIN Users u2        ON u2.Id            = m.Team1Player2Id
LEFT JOIN Users u3        ON u3.Id            = m.Team2Player1Id
LEFT JOIN Users u4        ON u4.Id            = m.Team2Player2Id
LEFT JOIN Users ub        ON ub.Id            = m.CreatedById
ORDER BY m.CreatedAt DESC;
