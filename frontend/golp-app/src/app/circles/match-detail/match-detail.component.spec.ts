import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
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
  let router: jasmine.SpyObj<Router>;

  beforeEach(async () => {
    matchSvc = jasmine.createSpyObj('MatchService', ['getMatchDetail', 'deleteMatchAsSuperAdmin', 'editMatchResultAsSuperAdmin']);
    authSvc  = jasmine.createSpyObj('AuthService', ['getCurrentUserId', 'isSuperAdmin']);
    authSvc.getCurrentUserId.and.returnValue(CURRENT_USER);
    authSvc.isSuperAdmin.and.returnValue(false);

    await TestBed.configureTestingModule({
      imports: [MatchDetailComponent],
      providers: [
        provideRouter([]),
        { provide: MatchService, useValue: matchSvc },
        { provide: AuthService,  useValue: authSvc },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: (k: string) => (k === 'circleId' ? CIRCLE_ID : MATCH_ID) } } },
        },
      ],
    }).compileComponents();

    router = TestBed.inject(Router) as jasmine.SpyObj<Router>;
    spyOn(router, 'navigate');
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

  // ─── US-061: cancellazione partita da super admin ──────────────────────────

  it('utente normale: pulsante "Cancella partita" non visibile', () => {
    authSvc.isSuperAdmin.and.returnValue(false);
    matchSvc.getMatchDetail.and.returnValue(of(makeMatch({ status: 'confirmed' })));
    const fixture = TestBed.createComponent(MatchDetailComponent);
    fixture.detectChanges();

    const html = fixture.nativeElement.textContent;
    expect(html).not.toContain('Cancella partita');
  });

  it('super admin: pulsante "Cancella partita" visibile, apre dialog di conferma senza chiamare subito il DELETE', () => {
    authSvc.isSuperAdmin.and.returnValue(true);
    matchSvc.getMatchDetail.and.returnValue(of(makeMatch({ status: 'confirmed' })));
    const fixture = TestBed.createComponent(MatchDetailComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Cancella partita');
    fixture.componentInstance.openDeleteConfirm();
    fixture.detectChanges();

    expect(fixture.componentInstance.showDeleteConfirm).toBeTrue();
    expect(matchSvc.deleteMatchAsSuperAdmin).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Conferma cancellazione');
  });

  it('super admin: conferma cancellazione chiama il DELETE e naviga allo storico', () => {
    authSvc.isSuperAdmin.and.returnValue(true);
    matchSvc.getMatchDetail.and.returnValue(of(makeMatch({ status: 'confirmed' })));
    matchSvc.deleteMatchAsSuperAdmin.and.returnValue(of(undefined));
    const fixture = TestBed.createComponent(MatchDetailComponent);
    fixture.detectChanges();

    fixture.componentInstance.confirmDelete();

    expect(matchSvc.deleteMatchAsSuperAdmin).toHaveBeenCalledWith(CIRCLE_ID, MATCH_ID);
    expect(router.navigate).toHaveBeenCalledWith(['/circles', CIRCLE_ID, 'matches']);
  });

  it('super admin: DELETE fallito mostra errore e non naviga', () => {
    authSvc.isSuperAdmin.and.returnValue(true);
    matchSvc.getMatchDetail.and.returnValue(of(makeMatch({ status: 'confirmed' })));
    matchSvc.deleteMatchAsSuperAdmin.and.returnValue(throwError(() => ({ status: 403 })));
    const fixture = TestBed.createComponent(MatchDetailComponent);
    fixture.detectChanges();

    fixture.componentInstance.confirmDelete();
    fixture.detectChanges();

    expect(fixture.componentInstance.deleteError).toContain('fallita');
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('annulla cancellazione chiude il dialog senza chiamare il DELETE', () => {
    authSvc.isSuperAdmin.and.returnValue(true);
    matchSvc.getMatchDetail.and.returnValue(of(makeMatch({ status: 'confirmed' })));
    const fixture = TestBed.createComponent(MatchDetailComponent);
    fixture.detectChanges();

    fixture.componentInstance.openDeleteConfirm();
    fixture.componentInstance.cancelDelete();

    expect(fixture.componentInstance.showDeleteConfirm).toBeFalse();
    expect(matchSvc.deleteMatchAsSuperAdmin).not.toHaveBeenCalled();
  });

  // ─── US-062: modifica risultato da super admin ─────────────────────────────

  it('utente normale: pulsante "Modifica risultato" non visibile', () => {
    authSvc.isSuperAdmin.and.returnValue(false);
    matchSvc.getMatchDetail.and.returnValue(of(makeMatch({ status: 'confirmed' })));
    const fixture = TestBed.createComponent(MatchDetailComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).not.toContain('Modifica risultato');
  });

  it('super admin, partita pending: pulsante "Modifica risultato" non visibile (solo confirmed)', () => {
    authSvc.isSuperAdmin.and.returnValue(true);
    matchSvc.getMatchDetail.and.returnValue(of(makeMatch({ status: 'pending' })));
    const fixture = TestBed.createComponent(MatchDetailComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).not.toContain('Modifica risultato');
  });

  it('super admin, partita confirmed: apre il form pre-popolato con i set correnti', () => {
    authSvc.isSuperAdmin.and.returnValue(true);
    matchSvc.getMatchDetail.and.returnValue(of(makeMatch({
      status: 'confirmed',
      sets: [{ team1Score: 6, team2Score: 4 }, { team1Score: 3, team2Score: 6 }],
    })));
    const fixture = TestBed.createComponent(MatchDetailComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Modifica risultato');
    fixture.componentInstance.openEditForm();
    fixture.detectChanges();

    expect(fixture.componentInstance.showEditForm).toBeTrue();
    expect(fixture.componentInstance.editSets).toEqual([{ team1: 6, team2: 4 }, { team1: 3, team2: 6 }]);
    expect(matchSvc.editMatchResultAsSuperAdmin).not.toHaveBeenCalled();
  });

  it('super admin: conferma modifica chiama la PUT con i set aggiornati e ricarica il dettaglio', () => {
    authSvc.isSuperAdmin.and.returnValue(true);
    const match = makeMatch({ status: 'confirmed', sets: [{ team1Score: 6, team2Score: 4 }] });
    matchSvc.getMatchDetail.and.returnValue(of(match));
    matchSvc.editMatchResultAsSuperAdmin.and.returnValue(of({ id: MATCH_ID, status: 'confirmed', winnerTeam: 2 }));
    const fixture = TestBed.createComponent(MatchDetailComponent);
    fixture.detectChanges();

    fixture.componentInstance.openEditForm();
    fixture.componentInstance.editSets = [{ team1: 2, team2: 6 }];
    fixture.componentInstance.confirmEdit();

    expect(matchSvc.editMatchResultAsSuperAdmin).toHaveBeenCalledWith(CIRCLE_ID, MATCH_ID, [{ team1: 2, team2: 6 }]);
    expect(fixture.componentInstance.showEditForm).toBeFalse();
    expect(matchSvc.getMatchDetail).toHaveBeenCalledTimes(2);
  });

  it('super admin: PUT fallita mostra errore e mantiene il form aperto', () => {
    authSvc.isSuperAdmin.and.returnValue(true);
    matchSvc.getMatchDetail.and.returnValue(of(makeMatch({ status: 'confirmed' })));
    matchSvc.editMatchResultAsSuperAdmin.and.returnValue(throwError(() => ({ status: 400 })));
    const fixture = TestBed.createComponent(MatchDetailComponent);
    fixture.detectChanges();

    fixture.componentInstance.openEditForm();
    fixture.componentInstance.confirmEdit();
    fixture.detectChanges();

    expect(fixture.componentInstance.editError).toContain('fallita');
    expect(fixture.componentInstance.showEditForm).toBeTrue();
  });

  it('annulla modifica chiude il form senza chiamare la PUT', () => {
    authSvc.isSuperAdmin.and.returnValue(true);
    matchSvc.getMatchDetail.and.returnValue(of(makeMatch({ status: 'confirmed' })));
    const fixture = TestBed.createComponent(MatchDetailComponent);
    fixture.detectChanges();

    fixture.componentInstance.openEditForm();
    fixture.componentInstance.cancelEdit();

    expect(fixture.componentInstance.showEditForm).toBeFalse();
    expect(matchSvc.editMatchResultAsSuperAdmin).not.toHaveBeenCalled();
  });
});
