import { pickActiveCircle, computeCurrentWinStreak, computeAggregateWinRate } from './dashboard.utils';
import { CircleSummary } from '../circles/circle.service';
import { MatchSummary, MyMatchSummary } from '../circles/match.service';

function circle(id: string, joinedAt: string): CircleSummary {
  return {
    id, name: id, sport: 'padel', sets: true, pointUnit: 'games',
    ownerId: 'owner', memberCount: 4, myRating: 1000, myRank: 1, joinedAt,
  };
}

function match(opts: { winnerTeam: number; status: 'pending' | 'confirmed' | 'disputed'; createdAt: string; userInTeam: 1 | 2 }): MatchSummary {
  const userId = 'u1';
  return {
    id: 'm', status: opts.status, winnerTeam: opts.winnerTeam, createdAt: opts.createdAt,
    myDelta: null, confirmationsCount: 4, hasCurrentUserConfirmed: true,
    team1: opts.userInTeam === 1 ? [{ userId, name: 'Me' }] : [{ userId: 'other', name: 'Other' }],
    team2: opts.userInTeam === 2 ? [{ userId, name: 'Me' }] : [{ userId: 'other', name: 'Other' }],
  };
}

describe('pickActiveCircle', () => {
  it('ritorna null se nessun circolo', () => {
    expect(pickActiveCircle([])).toBeNull();
  });

  it('ritorna l\'unico circolo se ce n\'è uno solo', () => {
    const c = circle('c1', '2026-01-01T00:00:00Z');
    expect(pickActiveCircle([c])).toBe(c);
  });

  it('ritorna il circolo con joinedAt più vecchio tra più circoli', () => {
    const older = circle('c1', '2026-01-01T00:00:00Z');
    const newer = circle('c2', '2026-03-01T00:00:00Z');
    expect(pickActiveCircle([newer, older])).toBe(older);
  });

  it('è deterministico se joinedAt è uguale (primo in ordine di array)', () => {
    const a = circle('c1', '2026-01-01T00:00:00Z');
    const b = circle('c2', '2026-01-01T00:00:00Z');
    expect(pickActiveCircle([a, b])).toBe(a);
  });
});

describe('computeCurrentWinStreak', () => {
  const userId = 'u1';

  it('ritorna 0 se nessuna partita', () => {
    expect(computeCurrentWinStreak([], userId)).toBe(0);
  });

  it('conta 3 vittorie consecutive più recenti, interrotte da una sconfitta più vecchia', () => {
    const matches: MatchSummary[] = [
      match({ winnerTeam: 1, status: 'confirmed', createdAt: '2026-01-04T00:00:00Z', userInTeam: 1 }),
      match({ winnerTeam: 1, status: 'confirmed', createdAt: '2026-01-03T00:00:00Z', userInTeam: 1 }),
      match({ winnerTeam: 1, status: 'confirmed', createdAt: '2026-01-02T00:00:00Z', userInTeam: 1 }),
      match({ winnerTeam: 2, status: 'confirmed', createdAt: '2026-01-01T00:00:00Z', userInTeam: 1 }),
    ];
    expect(computeCurrentWinStreak(matches, userId)).toBe(3);
  });

  it('ritorna 0 se l\'ultima partita non è una vittoria', () => {
    const matches: MatchSummary[] = [
      match({ winnerTeam: 2, status: 'confirmed', createdAt: '2026-01-02T00:00:00Z', userInTeam: 1 }),
      match({ winnerTeam: 1, status: 'confirmed', createdAt: '2026-01-01T00:00:00Z', userInTeam: 1 }),
    ];
    expect(computeCurrentWinStreak(matches, userId)).toBe(0);
  });

  it('conta tutte le vittorie se non ci sono sconfitte', () => {
    const matches: MatchSummary[] = [
      match({ winnerTeam: 1, status: 'confirmed', createdAt: '2026-01-03T00:00:00Z', userInTeam: 1 }),
      match({ winnerTeam: 1, status: 'confirmed', createdAt: '2026-01-02T00:00:00Z', userInTeam: 1 }),
      match({ winnerTeam: 1, status: 'confirmed', createdAt: '2026-01-01T00:00:00Z', userInTeam: 1 }),
    ];
    expect(computeCurrentWinStreak(matches, userId)).toBe(3);
  });

  it('ignora partite non confirmed', () => {
    const matches: MatchSummary[] = [
      match({ winnerTeam: 1, status: 'pending', createdAt: '2026-01-03T00:00:00Z', userInTeam: 1 }),
      match({ winnerTeam: 1, status: 'confirmed', createdAt: '2026-01-02T00:00:00Z', userInTeam: 1 }),
    ];
    expect(computeCurrentWinStreak(matches, userId)).toBe(1);
  });
});

function myMatch(opts: { myTeam: 1 | 2; winnerTeam: number; status: 'pending' | 'confirmed' | 'disputed' }): MyMatchSummary {
  return {
    matchId: 'm', circleId: 'c1', circleName: 'Circolo', sport: 'padel',
    createdAt: '2026-01-01T00:00:00Z', status: opts.status, winnerTeam: opts.winnerTeam,
    myTeam: opts.myTeam, sets: [], myDelta: null, confirmationsCount: 4, hasCurrentUserConfirmed: true,
    team1: [], team2: [],
  };
}

describe('computeAggregateWinRate', () => {
  it('ritorna 0 se nessuna partita', () => {
    expect(computeAggregateWinRate([])).toBe(0);
  });

  it('ritorna 100 se tutte vinte', () => {
    const matches = [
      myMatch({ myTeam: 1, winnerTeam: 1, status: 'confirmed' }),
      myMatch({ myTeam: 2, winnerTeam: 2, status: 'confirmed' }),
    ];
    expect(computeAggregateWinRate(matches)).toBe(100);
  });

  it('ritorna 0 se tutte perse', () => {
    const matches = [
      myMatch({ myTeam: 1, winnerTeam: 2, status: 'confirmed' }),
      myMatch({ myTeam: 2, winnerTeam: 1, status: 'confirmed' }),
    ];
    expect(computeAggregateWinRate(matches)).toBe(0);
  });

  it('calcola percentuale arrotondata su mix vinte/perse (3 su 4 -> 75)', () => {
    const matches = [
      myMatch({ myTeam: 1, winnerTeam: 1, status: 'confirmed' }),
      myMatch({ myTeam: 1, winnerTeam: 1, status: 'confirmed' }),
      myMatch({ myTeam: 1, winnerTeam: 1, status: 'confirmed' }),
      myMatch({ myTeam: 1, winnerTeam: 2, status: 'confirmed' }),
    ];
    expect(computeAggregateWinRate(matches)).toBe(75);
  });

  it('ignora partite non confirmed nel calcolo', () => {
    const matches = [
      myMatch({ myTeam: 1, winnerTeam: 1, status: 'confirmed' }),
      myMatch({ myTeam: 1, winnerTeam: 2, status: 'pending' }),
    ];
    expect(computeAggregateWinRate(matches)).toBe(100);
  });
});
