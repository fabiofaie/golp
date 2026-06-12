import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { CircleLeaderboardComponent } from './circle-leaderboard.component';
import { CircleService, LeaderboardEntry, LeaderboardResponse } from '../circle.service';
import { AuthService } from '../../auth/auth.service';

const CIRCLE_ID = 'circle-1';
const CURRENT_USER = 'user-current';

function makeEntry(overrides: Partial<LeaderboardEntry> = {}): LeaderboardEntry {
  return { userId: 'user-1', name: 'Luca', rating: 1050, rank: 1, confirmedMatches: 5, ...overrides };
}

function makeResponse(overrides: Partial<LeaderboardResponse> = {}): LeaderboardResponse {
  return { classified: [], unclassified: [], ...overrides };
}

describe('CircleLeaderboardComponent', () => {
  let circleSvc: jasmine.SpyObj<CircleService>;
  let authSvc: jasmine.SpyObj<AuthService>;

  beforeEach(async () => {
    circleSvc = jasmine.createSpyObj('CircleService', ['getLeaderboard']);
    authSvc   = jasmine.createSpyObj('AuthService', ['getCurrentUserId']);
    authSvc.getCurrentUserId.and.returnValue(CURRENT_USER);

    await TestBed.configureTestingModule({
      imports: [CircleLeaderboardComponent],
      providers: [
        { provide: CircleService, useValue: circleSvc },
        { provide: AuthService, useValue: authSvc },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { params: { circleId: CIRCLE_ID } } },
        },
      ],
    }).compileComponents();
  });

  it('should create', () => {
    circleSvc.getLeaderboard.and.returnValue(of(makeResponse()));
    const fixture = TestBed.createComponent(CircleLeaderboardComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('shows loading state initially', () => {
    // delay response so loading stays true during check
    circleSvc.getLeaderboard.and.returnValue(of(makeResponse()));
    const fixture = TestBed.createComponent(CircleLeaderboardComponent);
    // before detectChanges, loading = true
    expect(fixture.componentInstance.loading).toBeTrue();
  });

  it('current user row gets leaderboard-row--current class', () => {
    const entries: LeaderboardEntry[] = [
      makeEntry({ userId: 'user-other', rank: 1, rating: 1100 }),
      makeEntry({ userId: CURRENT_USER, rank: 2, rating: 1050 }),
    ];
    circleSvc.getLeaderboard.and.returnValue(of(makeResponse({ classified: entries })));

    const fixture = TestBed.createComponent(CircleLeaderboardComponent);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;

    const rows = el.querySelectorAll('.leaderboard-row');
    expect(rows.length).toBe(2);
    expect(rows[0].classList).not.toContain('leaderboard-row--current');
    expect(rows[1].classList).toContain('leaderboard-row--current');
  });

  it('unclassified section absent when unclassified=[]', () => {
    circleSvc.getLeaderboard.and.returnValue(of(makeResponse({
      classified: [makeEntry()],
      unclassified: [],
    })));

    const fixture = TestBed.createComponent(CircleLeaderboardComponent);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;

    expect(el.querySelector('.unclassified-section')).toBeNull();
  });

  it('unclassified section visible when there are unclassified players', () => {
    circleSvc.getLeaderboard.and.returnValue(of(makeResponse({
      classified: [makeEntry()],
      unclassified: [{ userId: 'user-x', name: 'Roberto' }],
    })));

    const fixture = TestBed.createComponent(CircleLeaderboardComponent);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;

    expect(el.querySelector('.unclassified-section')).not.toBeNull();
    expect(el.querySelector('.unclassified-section')!.textContent).toContain('Roberto');
  });

  it('current user in unclassified section gets current class', () => {
    circleSvc.getLeaderboard.and.returnValue(of(makeResponse({
      classified: [],
      unclassified: [
        { userId: 'user-other', name: 'Elena' },
        { userId: CURRENT_USER, name: 'Tu stesso' },
      ],
    })));

    const fixture = TestBed.createComponent(CircleLeaderboardComponent);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;

    const rows = el.querySelectorAll('.unclassified-row');
    expect(rows[1].classList).toContain('unclassified-row--current');
  });

  it('shows error message on service failure', () => {
    circleSvc.getLeaderboard.and.returnValue(throwError(() => new Error('net')));

    const fixture = TestBed.createComponent(CircleLeaderboardComponent);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;

    expect(fixture.componentInstance.errorMessage).toBeTruthy();
    expect(el.querySelector('.form-error')).not.toBeNull();
  });

  it('shows empty state when both arrays are empty', () => {
    circleSvc.getLeaderboard.and.returnValue(of(makeResponse({ classified: [], unclassified: [] })));

    const fixture = TestBed.createComponent(CircleLeaderboardComponent);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;

    expect(el.querySelector('.empty-state')).not.toBeNull();
  });

  it('isCurrentUser returns true only for matching userId', () => {
    circleSvc.getLeaderboard.and.returnValue(of(makeResponse()));
    const fixture = TestBed.createComponent(CircleLeaderboardComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    expect(comp.isCurrentUser(CURRENT_USER)).toBeTrue();
    expect(comp.isCurrentUser('someone-else')).toBeFalse();
  });
});
