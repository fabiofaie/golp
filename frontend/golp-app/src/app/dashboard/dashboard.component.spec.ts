import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { DashboardComponent } from './dashboard.component';
import { AuthService } from '../auth/auth.service';
import { CircleService, CircleSummary } from '../circles/circle.service';
import { MatchService, MyMatchSummary, MatchSummary, PagedResult } from '../circles/match.service';

const USER_ID = 'u1';

const CIRCLE_OK: CircleSummary = {
  id: 'c1', name: 'Padel Club Roma', sport: 'padel', sets: true, pointUnit: 'games',
  ownerId: 'owner', memberCount: 4, myRating: 1142, myRank: 3, joinedAt: '2026-01-01T00:00:00Z',
};

const CIRCLE_LOW_MEMBERS: CircleSummary = {
  id: 'c2', name: 'Amici del Martedì', sport: 'padel', sets: true, pointUnit: 'games',
  ownerId: 'owner', memberCount: 2, myRating: 1000, myRank: 1, joinedAt: '2026-01-01T00:00:00Z',
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

async function setup(opts: {
  circles: CircleSummary[];
  recentMatches?: MatchSummary[];
  pending?: MyMatchSummary[];
}) {
  const circleServiceMock = {
    getMyCircles: jasmine.createSpy('getMyCircles').and.returnValue(of(opts.circles)),
  };
  const matchServiceMock = {
    getMatches: jasmine.createSpy('getMatches').and.returnValue(of(opts.recentMatches ?? [])),
    getMyMatches: jasmine.createSpy('getMyMatches').and.returnValue(
      of({ totalCount: (opts.pending ?? []).length, page: 1, pageSize: 20, items: opts.pending ?? [] } as PagedResult<MyMatchSummary>)
    ),
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
      { provide: MatchService, useValue: matchServiceMock },
      { provide: AuthService, useValue: authServiceMock },
    ],
  }).compileComponents();

  const fixture: ComponentFixture<DashboardComponent> = TestBed.createComponent(DashboardComponent);
  fixture.detectChanges();
  return { fixture, component: fixture.componentInstance };
}

describe('DashboardComponent', () => {
  it('sceglie il circolo attivo corretto (unico circolo)', async () => {
    const { component } = await setup({ circles: [CIRCLE_OK] });
    expect(component.activeCircle()?.id).toBe('c1');
  });

  it('nasconde la sezione azioni urgenti se non ci sono richieste pending', async () => {
    const { component } = await setup({ circles: [CIRCLE_OK], pending: [] });
    expect(component.urgentMatches().length).toBe(0);
  });

  it('mostra le azioni urgenti quando presenti, cross-circolo', async () => {
    const { component } = await setup({ circles: [CIRCLE_OK], pending: [pendingMatch()] });
    expect(component.urgentMatches().length).toBe(1);
  });

  it('calcola la serie vittorie corrente dalle partite confirmed del circolo attivo', async () => {
    const { component } = await setup({ circles: [CIRCLE_OK], recentMatches: [confirmedMatch(18), confirmedMatch(12)] });
    expect(component.winStreak()).toBe(2);
  });

  it('il CTA (via ActiveCircleService) naviga sempre a Quick Match', async () => {
    const { component } = await setup({ circles: [CIRCLE_LOW_MEMBERS] });
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigate');

    component.activeCircleService.onRecordMatchClick();

    expect(navigateSpy).toHaveBeenCalledWith(['/match/quick']);
  });
});
