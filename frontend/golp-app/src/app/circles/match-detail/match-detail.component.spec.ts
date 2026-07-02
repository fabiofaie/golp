import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { MatchDetailComponent } from './match-detail.component';
import { MatchService, MatchDetail } from '../match.service';
import { AuthService } from '../../auth/auth.service';

const CIRCLE_ID  = 'circle-1';
const MATCH_ID   = 'match-1';
const CURRENT_USER = 'user-1';

function makeMatch(overrides: Partial<MatchDetail> = {}): MatchDetail {
  return {
    id: MATCH_ID,
    status: 'pending',
    winnerTeam: 1,
    createdAt: '2026-06-22T19:30:00Z',
    myDelta: null,
    confirmationsCount: 2,
    hasCurrentUserConfirmed: true,
    isParticipant: true,
    sets: [{ team1Score: 6, team2Score: 4 }],
    confirmedAt: null,
    confirmedByName: null,
    isForced: null,
    deltas: null,
    confirmations: [],
    confirmationLinks: null,
    team1: [{ userId: CURRENT_USER, name: 'Marco' }, { userId: 'user-2', name: 'Luca' }],
    team2: [{ userId: 'user-3', name: 'Sara' }, { userId: 'user-4', name: 'Giorgio' }],
    ...overrides,
  };
}

describe('MatchDetailComponent', () => {
  let matchSvc: jasmine.SpyObj<MatchService>;
  let authSvc: jasmine.SpyObj<AuthService>;

  beforeEach(async () => {
    matchSvc = jasmine.createSpyObj('MatchService', ['getMatchDetail']);
    authSvc  = jasmine.createSpyObj('AuthService', ['getCurrentUserId']);
    authSvc.getCurrentUserId.and.returnValue(CURRENT_USER);

    await TestBed.configureTestingModule({
      imports: [MatchDetailComponent],
      providers: [
        { provide: MatchService, useValue: matchSvc },
        { provide: AuthService,  useValue: authSvc },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: (k: string) => (k === 'circleId' ? CIRCLE_ID : MATCH_ID) } } },
        },
      ],
    }).compileComponents();
  });

  it('should create', () => {
    matchSvc.getMatchDetail.and.returnValue(of(makeMatch()));
    const fixture = TestBed.createComponent(MatchDetailComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });

  // ─── stato: pending (nessun delta, nessuna conferma decisiva) ──────────────

  it('pending: non mostra delta né dati di conferma', () => {
    matchSvc.getMatchDetail.and.returnValue(of(makeMatch({ status: 'pending' })));
    const fixture = TestBed.createComponent(MatchDetailComponent);
    fixture.detectChanges();

    expect(fixture.componentInstance.state).toBe('ready');
    expect(fixture.componentInstance.match?.deltas).toBeNull();
    const html = fixture.nativeElement.textContent;
    expect(html).toContain('In attesa di conferma');
    expect(html).not.toContain('Variazione rating');
  });

  // ─── stato: confirmed da 4° giocatore ───────────────────────────────────────

  it('confirmed da giocatore: mostra autore/data conferma e delta, nessun badge "Forzata"', () => {
    const match = makeMatch({
      status: 'confirmed',
      confirmedAt: '2026-06-22T20:12:00Z',
      confirmedByName: 'Giorgio',
      isForced: false,
      deltas: [
        { userId: CURRENT_USER, delta: 18 },
        { userId: 'user-2', delta: 15 },
        { userId: 'user-3', delta: -16 },
        { userId: 'user-4', delta: -17 },
      ],
    });
    matchSvc.getMatchDetail.and.returnValue(of(match));
    const fixture = TestBed.createComponent(MatchDetailComponent);
    fixture.detectChanges();

    const html = fixture.nativeElement.textContent;
    expect(html).toContain('Confermata da Giorgio');
    expect(html).toContain('Variazione rating');
    expect(html).not.toContain('Forzata');
  });

  // ─── stato: confirmed da forzatura proprietario ────────────────────────────

  it('confirmed da forzatura: mostra badge "Forzata" e nome proprietario', () => {
    const match = makeMatch({
      status: 'confirmed',
      confirmedAt: '2026-06-23T09:00:00Z',
      confirmedByName: 'Marco',
      isForced: true,
      deltas: [
        { userId: CURRENT_USER, delta: 18 },
        { userId: 'user-2', delta: 15 },
        { userId: 'user-3', delta: -16 },
        { userId: 'user-4', delta: -17 },
      ],
    });
    matchSvc.getMatchDetail.and.returnValue(of(match));
    const fixture = TestBed.createComponent(MatchDetailComponent);
    fixture.detectChanges();

    const html = fixture.nativeElement.textContent;
    expect(html).toContain('Confermata da Marco');
    expect(html).toContain('Forzata');
  });

  // ─── stato: disputed ────────────────────────────────────────────────────────

  it('disputed: non mostra delta né dati di conferma', () => {
    matchSvc.getMatchDetail.and.returnValue(of(makeMatch({ status: 'disputed' })));
    const fixture = TestBed.createComponent(MatchDetailComponent);
    fixture.detectChanges();

    const html = fixture.nativeElement.textContent;
    expect(html).toContain('Contestata');
    expect(html).not.toContain('Variazione rating');
  });

  // ─── errore di accesso (403/404) ────────────────────────────────────────────

  it('errore caricamento → stato error', () => {
    matchSvc.getMatchDetail.and.returnValue(throwError(() => ({ status: 403 })));
    const fixture = TestBed.createComponent(MatchDetailComponent);
    fixture.detectChanges();

    expect(fixture.componentInstance.state).toBe('error');
  });
});
