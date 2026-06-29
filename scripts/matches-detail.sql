-- Partite con nomi giocatori in chiaro e risultati set
SELECT
    m.Id                        AS MatchId,
    m.CreatedAt,
    m.Status,
    m.WinnerTeam,
    c.Name                      AS Circle,

    u1.Name                     AS T1P1_Name,
    u1.Email                    AS T1P1_Email,
    m.DeltaTeam1Player1         AS T1P1_Delta,

    u2.Name                     AS T1P2_Name,
    u2.Email                    AS T1P2_Email,
    m.DeltaTeam1Player2         AS T1P2_Delta,

    u3.Name                     AS T2P1_Name,
    u3.Email                    AS T2P1_Email,
    m.DeltaTeam2Player1         AS T2P1_Delta,

    u4.Name                     AS T2P2_Name,
    u4.Email                    AS T2P2_Email,
    m.DeltaTeam2Player2         AS T2P2_Delta,

    MAX(CASE WHEN ms.SetNumber = 1
        THEN CAST(ms.Team1Score AS VARCHAR(10)) + '-' + CAST(ms.Team2Score AS VARCHAR(10))
        END)                    AS Set1,
    MAX(CASE WHEN ms.SetNumber = 2
        THEN CAST(ms.Team1Score AS VARCHAR(10)) + '-' + CAST(ms.Team2Score AS VARCHAR(10))
        END)                    AS Set2,
    MAX(CASE WHEN ms.SetNumber = 3
        THEN CAST(ms.Team1Score AS VARCHAR(10)) + '-' + CAST(ms.Team2Score AS VARCHAR(10))
        END)                    AS Set3

FROM Matches m
JOIN Circles   c  ON c.Id  = m.CircleId
JOIN Users     u1 ON u1.Id = m.Team1Player1Id
JOIN Users     u2 ON u2.Id = m.Team1Player2Id
JOIN Users     u3 ON u3.Id = m.Team2Player1Id
JOIN Users     u4 ON u4.Id = m.Team2Player2Id
LEFT JOIN MatchSets ms ON ms.MatchId = m.Id

GROUP BY
    m.Id,
    m.CreatedAt,
    m.Status,
    m.WinnerTeam,
    c.Name,
    u1.Name, u1.Email, m.DeltaTeam1Player1,
    u2.Name, u2.Email, m.DeltaTeam1Player2,
    u3.Name, u3.Email, m.DeltaTeam2Player1,
    u4.Name, u4.Email, m.DeltaTeam2Player2

ORDER BY m.CreatedAt DESC;
