import { CircleSummary } from '../circles/circle.service';
import { MatchSummary, MyMatchSummary } from '../circles/match.service';

/**
 * Criterio provvisorio in attesa del selettore persistito (US-066):
 * unico circolo -> quello; più circoli -> il più vecchio per data di iscrizione.
 */
export function pickActiveCircle(circles: CircleSummary[]): CircleSummary | null {
  if (circles.length === 0) return null;
  if (circles.length === 1) return circles[0];

  return circles.reduce((oldest, current) =>
    joinedAtMs(current) < joinedAtMs(oldest) ? current : oldest
  );
}

// joinedAt è opzionale solo per compatibilità con fixture di test di altri componenti:
// il backend lo popola sempre. epoch 0 = "più vecchio di tutti" è sicuro solo per questo.
function joinedAtMs(circle: CircleSummary): number {
  return circle.joinedAt ? new Date(circle.joinedAt).getTime() : 0;
}

/**
 * Sequenza di vittorie consecutive più recenti. Sconfitta o pareggio azzera la serie.
 * Considera solo partite confirmed, ordinate per data decrescente (più recente prima).
 */
export function computeCurrentWinStreak(matches: MatchSummary[], userId: string): number {
  const confirmed = matches
    .filter(m => m.status === 'confirmed')
    .slice()
    .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());

  let streak = 0;
  for (const match of confirmed) {
    const won = didUserWin(match, userId);
    if (won === null) break;
    if (!won) break;
    streak++;
  }
  return streak;
}

/**
 * Percentuale di vittorie (0-100, arrotondata) su partite confirmed cross-circolo (US-067).
 * Usa myTeam/winnerTeam di MyMatchSummary, già relativi all'utente corrente — nessun filtro per userId necessario.
 */
export function computeAggregateWinRate(matches: MyMatchSummary[]): number {
  const confirmed = matches.filter(m => m.status === 'confirmed');
  if (confirmed.length === 0) return 0;
  const wins = confirmed.filter(m => m.myTeam === m.winnerTeam).length;
  return Math.round((wins / confirmed.length) * 100);
}

export function didUserWin(match: MatchSummary, userId: string): boolean | null {
  const inTeam1 = match.team1.some(p => p.userId === userId);
  const inTeam2 = match.team2.some(p => p.userId === userId);
  if (!inTeam1 && !inTeam2) return null;
  if (match.winnerTeam === 1) return inTeam1;
  if (match.winnerTeam === 2) return inTeam2;
  return false;
}
