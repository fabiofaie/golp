import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { DashboardComponent } from './dashboard.component';
import { AuthService } from '../auth/auth.service';
import { CircleService, CircleSummary } from '../circles/circle.service';
import { MatchSummary, MyMatchSummary } from '../circles/match.service';
import { DashboardService, DashboardSummary, DashboardActiveCircle, DashboardAggregate } from './dashboard.service';

const USER_ID = 'u1';

const CIRCLE_OK: CircleSummary = {
  id: 'c1', name: 'Padel Club Roma', sport: 'padel', sets: true, pointUnit: 'games',
  ownerId: 'owner', memberCount: 4, myRating: 1142, myRank: 3, joinedAt: '2026-01-01T00:00:00Z',
};

const CIRCLE_LOW_MEMBERS: CircleSummary = {
  id: 'c2', name: 'Amici del Martedì', sport: 'padel', sets: true, pointUnit: 'games',
  ownerId: 'owner', memberCount: 2, myRating: 1000, myRank: 1, joinedAt: '2026-01-01T00:00:00Z',
};

const CIRCLE_LATER: CircleSummary = {
  id: 'c3', name: 'Beach Tennis Estate', sport: 'beach-tennis', sets: true, pointUnit: 'games',
  ownerId: 'owner', memberCount: 6, myRating: 1300, myRank: 1, joinedAt: '2026-03-01T00:00:00Z',
};

function confirmedMatch(myDelta: number): MatchSummary {
  return {
    id: 'm1', status: 'confirmed', winnerTeam: 1, createdAt: '2026-07-01T00:00:00Z',
    myDelta, confirmationsCount: 4, hasCurrentUserConfirmed: true,
    team1: [{ userId: USER_ID, name: 'Me' }], team2: [{ userId: 'other', name: 'Other' }],
  };
}

function pendingMatch(): MyMatchSummary {
  return {
    matchId: 'p1', circleId: 'c1', circleName: 'Padel Club Roma', sport: 'padel',
    createdAt: '2026-07-10T00:00:00Z', status: 'pending', winnerTeam: 1, myTeam: 1,
    sets: [], myDelta: null, confirmationsCount: 2, hasCurrentUserConfirmed: false,
    team1: [{ userId: USER_ID, name: 'Me' }], team2: [{ userId: 'other', name: 'Other' }],
  };
}

function disputedMatch(): MyMatchSummary {
  return {
    matchId: 'd1', circleId: 'c1', circleName: 'Padel Club Roma', sport: 'padel',
    createdAt: '2026-07-09T00:00:00Z', status: 'disputed', winnerTeam: 1, myTeam: 1,
    sets: [], myDelta: null, confirmationsCount: 3, hasCurrentUserConfirmed: true,
    team1: [{ userId: USER_ID, name: 'Me' }], team2: [{ userId: 'other', name: 'Other' }],
  };
}

function activeCircleView(circle: CircleSummary, recentMatches: MatchSummary[]): DashboardActiveCircle {
  return {
    id: circle.id, name: circle.name, sport: circle.sport,
    myRating: circle.myRating, myRank: circle.myRank, memberCount: circle.memberCount,
    confirmedMatchesCount: recentMatches.filter(m => m.status === 'confirmed').length,
    recentMatches,
  };
}

async function setup(opts: {
  circles: CircleSummary[];
  recentMatches?: MatchSummary[];
  urgentMatches?: MyMatchSummary[];
  aggregate?: DashboardAggregate;
}) {
  const circleServiceMock = {
    getMyCircles: jasmine.createSpy('getMyCircles').and.returnValue(of(opts.circles)),
  };

  const dashboardServiceMock = {
    getDashboardSummary: jasmine.createSpy('getDashboardSummary').and.callFake((circleId?: string) => {
      const isAll = !circleId;
      const summary: DashboardSummary = {
        circles: opts.circles,
        activeCircle: isAll ? null : activeCircleView(
          opts.circles.find(c => c.id === circleId) ?? opts.circles[0],
          opts.recentMatches ?? []
        ),
        aggregate: isAll ? (opts.aggregate ?? { circlesCount: opts.circles.length, confirmedMatchesCount: 0, winRate: 0 }) : null,
        urgentMatches: opts.urgentMatches ?? [],
      };
      return of(summary);
    }),
  };

  const authServiceMock = {
    getCurrentUserId: () => USER_ID,
    logout: jasmine.createSpy('logout'),
  };

  await TestBed.configureTestingModule({
    imports: [DashboardComponent],
    providers: [
      provideRouter([]),
      { provide: CircleService, useValue: circleServiceMock },
      { provide: DashboardService, useValue: dashboardServiceMock },
      { provide: AuthService, useValue: authServiceMock },
    ],
  }).compileComponents();

  const fixture: ComponentFixture<DashboardComponent> = TestBed.createComponent(DashboardComponent);
  fixture.detectChanges();
  return { fixture, component: fixture.componentInstance, dashboardServiceMock };
}

describe('DashboardComponent', () => {
  beforeEach(() => {
    localStorage.removeItem('golp_active_circle_id');
    localStorage.removeItem('golp_favorite_circle_ids');
  });

  it('sceglie il circolo attivo corretto (unico circolo)', async () => {
    const { component } = await setup({ circles: [CIRCLE_OK] });
    expect(component.activeCircle()?.id).toBe('c1');
  });

  it('nasconde la sezione azioni urgenti se non ci sono richieste pending', async () => {
    const { component } = await setup({ circles: [CIRCLE_OK], urgentMatches: [] });
    expect(component.urgentMatches().length).toBe(0);
  });

  it('mostra le azioni urgenti quando presenti, cross-circolo', async () => {
    const { component } = await setup({ circles: [CIRCLE_OK], urgentMatches: [pendingMatch()] });
    expect(component.urgentMatches().length).toBe(1);
  });

  it('mostra sia pending sia disputed contemporaneamente quando entrambe presenti (US-068 AC5)', async () => {
    const { component } = await setup({ circles: [CIRCLE_OK], urgentMatches: [pendingMatch(), disputedMatch()] });
    expect(component.urgentMatches().length).toBe(2);
    expect(component.urgentMatches().some(m => m.status === 'pending')).toBeTrue();
    expect(component.urgentMatches().some(m => m.status === 'disputed')).toBeTrue();
  });

  it('il conteggio urgentMatches somma pending e disputed (US-068 AC6)', async () => {
    const { component } = await setup({ circles: [CIRCLE_OK], urgentMatches: [pendingMatch(), disputedMatch()] });
    expect(component.urgentMatches().length).toBe(2);
  });

  it('calcola la serie vittorie corrente dalle partite confirmed del circolo attivo', async () => {
    const { component } = await setup({ circles: [CIRCLE_OK], recentMatches: [confirmedMatch(18), confirmedMatch(12)] });
    expect(component.winStreak()).toBe(2);
  });

  it('il conteggio "Partite" usa confirmedMatchesCount dal summary, non la lunghezza di recentMatches', async () => {
    const { component } = await setup({ circles: [CIRCLE_OK], recentMatches: [confirmedMatch(18)] });
    expect(component.confirmedMatchesCount()).toBe(1);
  });

  it('il CTA (via ActiveCircleService) naviga a Quick Match con il circolo attivo come default', async () => {
    const { component } = await setup({ circles: [CIRCLE_LOW_MEMBERS] });
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigate');

    component.activeCircleService.onRecordMatchClick();

    expect(navigateSpy).toHaveBeenCalledWith(['/match/quick'], { queryParams: { circleId: 'c2' } });
  });

  it('US-070: il caricamento della dashboard effettua una sola chiamata a getDashboardSummary', async () => {
    const { dashboardServiceMock } = await setup({ circles: [CIRCLE_OK], recentMatches: [confirmedMatch(18)] });
    expect(dashboardServiceMock.getDashboardSummary.calls.count()).toBe(1);
  });

  it('selezionare un circolo dal pannello aggiorna rating/posizione/partite recenti mostrati', async () => {
    const { component, fixture } = await setup({
      circles: [CIRCLE_OK, CIRCLE_LATER],
      recentMatches: [confirmedMatch(18)],
    });

    // fallback iniziale: circolo più vecchio (CIRCLE_OK)
    expect(component.activeCircle()?.id).toBe('c1');

    component.activeCircleService.selectCircle('c3');
    fixture.detectChanges();

    expect(component.activeCircle()?.id).toBe('c3');
    const ratingText = fixture.nativeElement.querySelector('.rating-main strong')?.textContent;
    expect(ratingText).toContain('1300');
  });

  it('selezionando "Tutti i circoli" chiama getDashboardSummary senza circleId e calcola aggregateStats', async () => {
    const { component, fixture, dashboardServiceMock } = await setup({
      circles: [CIRCLE_OK, CIRCLE_LATER],
      urgentMatches: [pendingMatch()],
      aggregate: { circlesCount: 2, confirmedMatchesCount: 3, winRate: 67 },
    });

    component.activeCircleService.selectCircle('all');
    fixture.detectChanges();

    expect(component.activeCircle()).toBeNull();
    expect(dashboardServiceMock.getDashboardSummary).toHaveBeenCalledWith(undefined);
    expect(component.aggregateStats()).toEqual({
      circlesCount: 2,
      confirmedMatchesCount: 3,
      winRate: 67,
      urgentCount: 1,
    });
  });

  it('non ri-effettua la fetch se lo stato "tutti i circoli" resta invariato', async () => {
    const { component, fixture, dashboardServiceMock } = await setup({
      circles: [CIRCLE_OK, CIRCLE_LATER],
    });

    component.activeCircleService.selectCircle('all');
    fixture.detectChanges();
    const callsAfterFirst = dashboardServiceMock.getDashboardSummary.calls.count();

    fixture.detectChanges();
    fixture.detectChanges();

    expect(dashboardServiceMock.getDashboardSummary.calls.count()).toBe(callsAfterFirst);
  });

  it('mostra contatori e card sintetiche per circolo in modalità "Tutti i circoli", senza alcun rating aggregato', async () => {
    const { component, fixture } = await setup({
      circles: [CIRCLE_OK, CIRCLE_LATER],
      aggregate: { circlesCount: 2, confirmedMatchesCount: 2, winRate: 50 },
    });

    component.activeCircleService.selectCircle('all');
    fixture.detectChanges();

    const statCards = fixture.nativeElement.querySelectorAll('.aggregate-stat');
    expect(statCards.length).toBe(4);

    const circleCards = fixture.nativeElement.querySelectorAll('.circle-summary-card');
    expect(circleCards.length).toBe(2);
    expect(circleCards[0].textContent).toContain(CIRCLE_OK.name);
    expect(circleCards[1].textContent).toContain(CIRCLE_LATER.name);

    // AC1 — nessun rating aggregato/somma tra circoli in nessun punto della vista
    const bodyText = fixture.nativeElement.textContent as string;
    expect(bodyText).not.toContain('Rating totale');
    expect(bodyText).not.toContain('RATING TOTALE');
    // le uniche occorrenze di myRating sono quelle per-circolo, non sommate
    expect(fixture.nativeElement.querySelectorAll('.rating-main').length).toBe(0);
  });

  it('distingue visivamente pending da disputed nella sezione azioni urgenti (US-068 AC1/AC2)', async () => {
    const { fixture } = await setup({ circles: [CIRCLE_OK], urgentMatches: [pendingMatch(), disputedMatch()] });
    fixture.detectChanges();

    const cards = fixture.nativeElement.querySelectorAll('.pending-card');
    expect(cards.length).toBe(2);

    const pendingCard = fixture.nativeElement.querySelector('.pending-card:not(.disputed)');
    const disputedCard = fixture.nativeElement.querySelector('.pending-card.disputed');
    expect(pendingCard).not.toBeNull();
    expect(disputedCard).not.toBeNull();
    expect(pendingCard.textContent).toContain('DA CONFERMARE');
    expect(disputedCard.textContent).toContain('CONTESTATA');
  });

  it('US-069 AC1 — nessun circolo mostra il messaggio con CTA, nessun'
    + ' elemento di dashboard-content renderizzato', async () => {
    const { fixture } = await setup({ circles: [] });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.auth-title')?.textContent).toContain('Ciao!');
    expect(fixture.nativeElement.querySelectorAll('a[routerLink="/circles/create"]').length).toBe(1);
    expect(fixture.nativeElement.querySelectorAll('a[routerLink="/circles/browse"]').length).toBe(1);

    // nessuna sezione della dashboard "piena" è presente in questo stato
    expect(fixture.nativeElement.querySelector('.dashboard-content')).toBeNull();
    expect(fixture.nativeElement.querySelector('.rating-card')).toBeNull();
    expect(fixture.nativeElement.querySelector('.attention')).toBeNull();
  });

  it('US-069 AC3 — nessuna richiesta urgente: la sezione "Richiede attenzione" è assente, non vuota', async () => {
    const { fixture } = await setup({ circles: [CIRCLE_OK], urgentMatches: [] });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.attention')).toBeNull();
  });

  it('US-069 AC5 — nessuna partita e circolo con meno di 4 membri invita a completare il circolo', async () => {
    const { fixture } = await setup({ circles: [CIRCLE_LOW_MEMBERS] });
    fixture.detectChanges();

    const emptyState = fixture.nativeElement.querySelector('.empty-state');
    expect(emptyState).not.toBeNull();
    expect(emptyState.textContent).toContain('Il circolo ha bisogno di altri giocatori');
    expect(emptyState.textContent).not.toContain('Registra la prima partita');
    expect(emptyState.querySelector('a[routerLink="/circles"]')).not.toBeNull();
  });

  it('US-069 AC2 — nessuna partita e circolo con almeno 4 membri invita a registrare la prima partita', async () => {
    const { fixture } = await setup({ circles: [CIRCLE_OK] }); // CIRCLE_OK ha memberCount 4
    fixture.detectChanges();

    const emptyState = fixture.nativeElement.querySelector('.empty-state');
    expect(emptyState).not.toBeNull();
    expect(emptyState.textContent).toContain('Nessuna partita ancora');
    expect(emptyState.textContent).not.toContain('bisogno di altri giocatori');
  });

  it('US-069 AC4 — nessuno stato vuoto (nessun circolo) mostra "undefined"/"null"/"NaN"', async () => {
    const { fixture } = await setup({ circles: [] });
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).not.toContain('undefined');
    expect(text).not.toContain('null');
    expect(text).not.toMatch(/\bNaN\b/);
  });

  it('US-069 AC4 — nessuno stato vuoto (circolo <4 membri) mostra "undefined"/"null"/"NaN"', async () => {
    const { fixture } = await setup({ circles: [CIRCLE_LOW_MEMBERS], urgentMatches: [] });
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).not.toContain('undefined');
    expect(text).not.toContain('null');
    expect(text).not.toMatch(/\bNaN\b/);
  });

  it('US-069 AC4 — nessuno stato vuoto (circolo pieno senza partite/urgenti) mostra "undefined"/"null"/"NaN"', async () => {
    const { fixture } = await setup({ circles: [CIRCLE_OK], urgentMatches: [] });
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).not.toContain('undefined');
    expect(text).not.toContain('null');
    expect(text).not.toMatch(/\bNaN\b/);
  });
});
