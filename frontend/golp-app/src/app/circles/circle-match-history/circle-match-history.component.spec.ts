import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of, throwError, Subject } from 'rxjs';
import { CircleMatchHistoryComponent } from './circle-match-history.component';
import { MatchService, MatchSummary } from '../match.service';
import { CircleService, CircleSummary } from '../circle.service';
import { AuthService } from '../../auth/auth.service';

const CIRCLE_ID = 'circle-1';
const CURRENT_USER = 'user-1';
const OWNER_ID = 'user-1';       // same as current user = owner scenario
const OTHER_USER = 'user-owner'; // different = non-owner scenario

function makeMatch(overrides: Partial<MatchSummary> = {}): MatchSummary {
  return {
    id: 'match-1',
    status: 'pending',
    winnerTeam: 1,
    createdAt: '2026-06-12T10:00:00Z',
    myDelta: null,
    confirmationsCount: 1,
    hasCurrentUserConfirmed: false,
    team1: [{ userId: CURRENT_USER, name: 'Marco' }, { userId: 'user-2', name: 'Luca' }],
    team2: [{ userId: 'user-3', name: 'Sara' }, { userId: 'user-4', name: 'Giorgio' }],
    ...overrides,
  };
}

function makeCircleSummary(ownerId: string): CircleSummary {
  return {
    id: CIRCLE_ID,
    name: 'Test Circle',
    sport: 'padel',
    sets: false,
    pointUnit: 'game',
    ownerId,
    memberCount: 4,
    myRating: 1000,
    myRank: 1,
  };
}

describe('CircleMatchHistoryComponent', () => {
  let matchSvc: jasmine.SpyObj<MatchService>;
  let circleSvc: jasmine.SpyObj<CircleService>;
  let authSvc: jasmine.SpyObj<AuthService>;

  beforeEach(async () => {
    matchSvc  = jasmine.createSpyObj('MatchService', ['getMatches', 'confirm', 'dispute', 'forceConfirm']);
    circleSvc = jasmine.createSpyObj('CircleService', ['getMyCircles']);
    authSvc   = jasmine.createSpyObj('AuthService', ['getCurrentUserId']);
    authSvc.getCurrentUserId.and.returnValue(CURRENT_USER);

    // default: current user is NOT owner
    circleSvc.getMyCircles.and.returnValue(of([makeCircleSummary(OTHER_USER)]));

    await TestBed.configureTestingModule({
      imports: [CircleMatchHistoryComponent],
      providers: [
        { provide: MatchService,  useValue: matchSvc },
        { provide: CircleService, useValue: circleSvc },
        { provide: AuthService,   useValue: authSvc },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => CIRCLE_ID } } },
        },
      ],
    }).compileComponents();
  });

  it('should create', () => {
    matchSvc.getMatches.and.returnValue(of([]));
    const fixture = TestBed.createComponent(CircleMatchHistoryComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });

  // ─── isOwner derivation ───────────────────────────────────────────────────

  it('isOwner is true when circle ownerId matches currentUserId', () => {
    circleSvc.getMyCircles.and.returnValue(of([makeCircleSummary(OWNER_ID)]));
    matchSvc.getMatches.and.returnValue(of([]));

    const fixture = TestBed.createComponent(CircleMatchHistoryComponent);
    fixture.detectChanges();

    expect(fixture.componentInstance.isOwner).toBeTrue();
  });

  it('isOwner is false when circle ownerId differs from currentUserId', () => {
    circleSvc.getMyCircles.and.returnValue(of([makeCircleSummary(OTHER_USER)]));
    matchSvc.getMatches.and.returnValue(of([]));

    const fixture = TestBed.createComponent(CircleMatchHistoryComponent);
    fixture.detectChanges();

    expect(fixture.componentInstance.isOwner).toBeFalse();
  });

  // ─── "Forza conferma" button ──────────────────────────────────────────────

  it('shows "Forza conferma" button for owner on pending match', () => {
    circleSvc.getMyCircles.and.returnValue(of([makeCircleSummary(OWNER_ID)]));
    matchSvc.getMatches.and.returnValue(of([makeMatch()]));

    const fixture = TestBed.createComponent(CircleMatchHistoryComponent);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;

    expect(el.querySelector('.btn-force-confirm')).toBeTruthy();
  });

  it('hides "Forza conferma" button for non-owner', () => {
    circleSvc.getMyCircles.and.returnValue(of([makeCircleSummary(OTHER_USER)]));
    matchSvc.getMatches.and.returnValue(of([makeMatch()]));

    const fixture = TestBed.createComponent(CircleMatchHistoryComponent);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;

    expect(el.querySelector('.btn-force-confirm')).toBeNull();
  });

  it('hides "Forza conferma" button for owner on confirmed match', () => {
    circleSvc.getMyCircles.and.returnValue(of([makeCircleSummary(OWNER_ID)]));
    matchSvc.getMatches.and.returnValue(of([makeMatch({ status: 'confirmed', confirmationsCount: 4 })]));

    const fixture = TestBed.createComponent(CircleMatchHistoryComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.btn-force-confirm')).toBeNull();
  });

  it('calls forceConfirm() on service and reloads on button click', () => {
    circleSvc.getMyCircles.and.returnValue(of([makeCircleSummary(OWNER_ID)]));
    const m = makeMatch();
    matchSvc.getMatches.and.returnValue(of([m]));
    matchSvc.forceConfirm.and.returnValue(of({ status: 'confirmed' }));

    const fixture = TestBed.createComponent(CircleMatchHistoryComponent);
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('.btn-force-confirm') as HTMLButtonElement).click();
    expect(matchSvc.forceConfirm).toHaveBeenCalledWith(CIRCLE_ID, m.id);
    expect(matchSvc.getMatches).toHaveBeenCalledTimes(2); // init + reload
  });

  // ─── existing tests (unchanged) ───────────────────────────────────────────

  it('shows confirm and dispute buttons for pending match where user has not confirmed', () => {
    const m = makeMatch({ hasCurrentUserConfirmed: false });
    matchSvc.getMatches.and.returnValue(of([m]));

    const fixture = TestBed.createComponent(CircleMatchHistoryComponent);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;

    expect(el.querySelector('.btn-confirm')).toBeTruthy();
    expect(el.querySelector('.btn-dispute')).toBeTruthy();
  });

  it('shows "Hai già confermato" for pending match where user has confirmed', () => {
    const m = makeMatch({ hasCurrentUserConfirmed: true, confirmationsCount: 2 });
    matchSvc.getMatches.and.returnValue(of([m]));

    const fixture = TestBed.createComponent(CircleMatchHistoryComponent);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;

    expect(el.querySelector('.btn-confirm')).toBeNull();
    expect(el.textContent).toContain('Hai già confermato');
  });

  it('hides actions for confirmed match', () => {
    const m = makeMatch({ status: 'confirmed', confirmationsCount: 4, hasCurrentUserConfirmed: true });
    matchSvc.getMatches.and.returnValue(of([m]));

    const fixture = TestBed.createComponent(CircleMatchHistoryComponent);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;

    expect(el.querySelector('.btn-confirm')).toBeNull();
    expect(el.querySelector('.btn-dispute')).toBeNull();
    expect(el.querySelector('.match-actions')).toBeNull();
  });

  it('renders 4 dots with 2 filled for confirmationsCount=2', () => {
    const m = makeMatch({ confirmationsCount: 2, hasCurrentUserConfirmed: false });
    matchSvc.getMatches.and.returnValue(of([m]));

    const fixture = TestBed.createComponent(CircleMatchHistoryComponent);
    fixture.detectChanges();
    const dots = fixture.nativeElement.querySelectorAll('.confirm-dot');

    expect(dots.length).toBe(4);
    const filled = Array.from(dots).filter((d: any) => d.classList.contains('confirm-dot--filled')).length;
    const empty  = Array.from(dots).filter((d: any) => d.classList.contains('confirm-dot--filled') === false
                                                     && d.classList.contains('confirm-dot--you') === false).length;
    expect(filled).toBe(2);
    expect(empty).toBe(2);
  });

  it('btn-confirm link navigates to match detail (RouterLink, no direct confirm() call)', () => {
    const m = makeMatch();
    matchSvc.getMatches.and.returnValue(of([m]));

    const fixture = TestBed.createComponent(CircleMatchHistoryComponent);
    fixture.detectChanges();

    const link: HTMLAnchorElement = fixture.nativeElement.querySelector('.btn-confirm');
    expect(link).toBeTruthy();
    // Confirmation happens on the detail page (match-confirm component)
    expect(matchSvc.confirm).not.toHaveBeenCalled();
  });

  it('calls dispute() on service and reloads on Contesta click', () => {
    const m = makeMatch();
    matchSvc.getMatches.and.returnValue(of([m]));
    matchSvc.dispute.and.returnValue(of({ status: 'disputed' }));

    const fixture = TestBed.createComponent(CircleMatchHistoryComponent);
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('.btn-dispute') as HTMLButtonElement).click();
    expect(matchSvc.dispute).toHaveBeenCalledWith(CIRCLE_ID, m.id);
    expect(matchSvc.getMatches).toHaveBeenCalledTimes(2);
  });

  it('shows error message when getMatches fails', () => {
    matchSvc.getMatches.and.returnValue(throwError(() => new Error('network')));

    const fixture = TestBed.createComponent(CircleMatchHistoryComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.form-error')).toBeTruthy();
  });

  it('shows "(Tu)" next to current user name', () => {
    const m = makeMatch();
    matchSvc.getMatches.and.returnValue(of([m]));

    const fixture = TestBed.createComponent(CircleMatchHistoryComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('(Tu)');
  });

  // US-009 — delta badge

  it('shows "+12 pt" green badge for positive delta on confirmed match', () => {
    const m = makeMatch({ status: 'confirmed', myDelta: 12, confirmationsCount: 4, hasCurrentUserConfirmed: true });
    matchSvc.getMatches.and.returnValue(of([m]));

    const fixture = TestBed.createComponent(CircleMatchHistoryComponent);
    fixture.detectChanges();
    const badge: HTMLElement = fixture.nativeElement.querySelector('.delta-badge');

    expect(badge).toBeTruthy();
    expect(badge.classList.contains('delta-badge--positive')).toBeTrue();
    expect(badge.textContent?.trim()).toBe('+12 pt');
  });

  it('shows "-8 pt" red badge for negative delta on confirmed match', () => {
    const m = makeMatch({ status: 'confirmed', myDelta: -8, confirmationsCount: 4, hasCurrentUserConfirmed: true });
    matchSvc.getMatches.and.returnValue(of([m]));

    const fixture = TestBed.createComponent(CircleMatchHistoryComponent);
    fixture.detectChanges();
    const badge: HTMLElement = fixture.nativeElement.querySelector('.delta-badge');

    expect(badge).toBeTruthy();
    expect(badge.classList.contains('delta-badge--negative')).toBeTrue();
    expect(badge.textContent?.trim()).toBe('-8 pt');
  });

  it('shows "+0 pt" neutral badge for zero delta', () => {
    const m = makeMatch({ status: 'confirmed', myDelta: 0, confirmationsCount: 4, hasCurrentUserConfirmed: true });
    matchSvc.getMatches.and.returnValue(of([m]));

    const fixture = TestBed.createComponent(CircleMatchHistoryComponent);
    fixture.detectChanges();
    const badge: HTMLElement = fixture.nativeElement.querySelector('.delta-badge');

    expect(badge).toBeTruthy();
    expect(badge.classList.contains('delta-badge--zero')).toBeTrue();
    expect(badge.textContent?.trim()).toBe('+0 pt');
  });

  it('shows no delta badge when myDelta is null', () => {
    const m = makeMatch({ status: 'confirmed', myDelta: null, confirmationsCount: 4, hasCurrentUserConfirmed: true });
    matchSvc.getMatches.and.returnValue(of([m]));

    const fixture = TestBed.createComponent(CircleMatchHistoryComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.delta-badge')).toBeNull();
  });

  it('shows "In attesa" badge for pending match (no delta badge)', () => {
    const m = makeMatch({ status: 'pending', myDelta: null });
    matchSvc.getMatches.and.returnValue(of([m]));

    const fixture = TestBed.createComponent(CircleMatchHistoryComponent);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;

    expect(el.textContent).toContain('In attesa');
    expect(el.querySelector('.delta-badge')).toBeNull();
  });

  it('shows loading state while matches are loading', () => {
    const pending$ = new Subject<MatchSummary[]>();
    matchSvc.getMatches.and.returnValue(pending$.asObservable());
    const fixture = TestBed.createComponent(CircleMatchHistoryComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Caricamento');
    pending$.complete();
  });
});
