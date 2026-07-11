import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { QuickMatchComponent } from './quick-match.component';
import { AuthService } from '../../auth/auth.service';
import { CircleService } from '../circle.service';
import { MatchService, SuggestionUser } from '../match.service';

function suggestion(userId: string, name: string, circles: { id: string; name: string }[]): SuggestionUser {
  return { userId, name, isActivated: true, circles };
}

describe('QuickMatchComponent — US-057 groupedSuggestions', () => {
  let component: QuickMatchComponent;

  beforeEach(async () => {
    const authSvc = jasmine.createSpyObj('AuthService', ['getCurrentUserId']);
    const circleSvc = jasmine.createSpyObj('CircleService', ['getSports']);
    const matchSvc = jasmine.createSpyObj('MatchService', ['getSuggestions']);

    await TestBed.configureTestingModule({
      imports: [QuickMatchComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AuthService, useValue: authSvc },
        { provide: CircleService, useValue: circleSvc },
        { provide: MatchService, useValue: matchSvc },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(QuickMatchComponent);
    component = fixture.componentInstance;
    // ngOnInit not triggered (no detectChanges): suggestions set directly for pure getter testing.
  });

  it('returns empty array when there are no suggestions', () => {
    component.suggestions = [];
    expect(component.groupedSuggestions).toEqual([]);
  });

  it('groups a user into one entry per shared circle', () => {
    component.suggestions = [
      suggestion('u1', 'Mario', [
        { id: 'c1', name: 'Circolo Alpha' },
        { id: 'c2', name: 'Circolo Beta' },
      ]),
    ];

    const groups = component.groupedSuggestions;
    expect(groups.length).toBe(2);
    expect(groups.map(g => g.label)).toEqual(['Circolo Alpha', 'Circolo Beta']);
    expect(groups[0].users.map(u => u.userId)).toEqual(['u1']);
    expect(groups[1].users.map(u => u.userId)).toEqual(['u1']);
  });

  it('puts users without shared circles under "Giocati di recente" first', () => {
    component.suggestions = [
      suggestion('u1', 'Mario', [{ id: 'c1', name: 'Circolo Beta' }]),
      suggestion('u2', 'Luigi', []),
    ];

    const groups = component.groupedSuggestions;
    expect(groups[0].label).toBe('Giocati di recente');
    expect(groups[0].users.map(u => u.userId)).toEqual(['u2']);
    expect(groups[1].label).toBe('Circolo Beta');
  });

  it('shows a single circle group with its label when the user has only one circle', () => {
    component.suggestions = [suggestion('u1', 'Mario', [{ id: 'c1', name: 'Solo Circolo' }])];

    const groups = component.groupedSuggestions;
    expect(groups.length).toBe(1);
    expect(groups[0].label).toBe('Solo Circolo');
    expect(groups[0].circleId).toBe('c1');
  });

  it('orders circle groups alphabetically by name', () => {
    component.suggestions = [
      suggestion('u1', 'A', [{ id: 'c2', name: 'Zeta' }]),
      suggestion('u2', 'B', [{ id: 'c1', name: 'Alpha' }]),
    ];

    const labels = component.groupedSuggestions.map(g => g.label);
    expect(labels).toEqual(['Alpha', 'Zeta']);
  });

  it('orders users within a group alphabetically by name, case-insensitive and accent-aware (US-072)', () => {
    component.suggestions = [
      suggestion('u1', 'Zeta', [{ id: 'c1', name: 'Circolo' }]),
      suggestion('u2', 'anna', [{ id: 'c1', name: 'Circolo' }]),
      suggestion('u3', 'Élena', [{ id: 'c1', name: 'Circolo' }]),
      suggestion('u4', 'Marco', [{ id: 'c1', name: 'Circolo' }]),
    ];

    const names = component.groupedSuggestions[0].users.map(u => u.name);
    expect(names).toEqual(['anna', 'Élena', 'Marco', 'Zeta']);
  });
});

describe('QuickMatchComponent — US-071 slot0 unlock', () => {
  let component: QuickMatchComponent;
  let matchSvc: jasmine.SpyObj<MatchService>;

  beforeEach(async () => {
    const authSvc = jasmine.createSpyObj('AuthService', ['getCurrentUserId']);
    const circleSvc = jasmine.createSpyObj('CircleService', ['getSports']);
    matchSvc = jasmine.createSpyObj('MatchService', ['getSuggestions', 'checkQuickMatch']);

    await TestBed.configureTestingModule({
      imports: [QuickMatchComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AuthService, useValue: authSvc },
        { provide: CircleService, useValue: circleSvc },
        { provide: MatchService, useValue: matchSvc },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(QuickMatchComponent);
    component = fixture.componentInstance;
    component.currentUserId = 'me-id';
    component.currentUserName = 'Io Stesso';
    component.selectedSport = { sport: 'basket2v2', displayName: 'Basket 2v2', pointUnit: 'punti', sets: false, teamSize: 2, allowsSingles: false } as any;
    component.slots[0] = { filled: true, userId: 'me-id', displayName: 'Io Stesso', isMe: true, isGuest: false };
  });

  it('allows clearing slot 0 (no longer locked on "me")', () => {
    component.clearSlot(0);
    expect(component.slots[0].filled).toBeFalse();
  });

  it('meAvailableToAdd is true once slot 0 is cleared and there is room', () => {
    component.clearSlot(0);
    expect(component.meAvailableToAdd).toBeTrue();
  });

  it('meAvailableToAdd is false when creator is already placed in a slot', () => {
    expect(component.meAvailableToAdd).toBeFalse();
  });

  it('addMe() re-fills slot 0 as isMe when it is the first empty active slot', () => {
    component.clearSlot(0);
    component.addMe();
    expect(component.slots[0]).toEqual(jasmine.objectContaining({ filled: true, userId: 'me-id', isMe: true }));
  });

  it('filters out circles not owned by the creator when creator is absent from slots', (done) => {
    component.clearSlot(0);
    component.slots[1] = { filled: true, userId: 'p2', displayName: 'P2', isMe: false, isGuest: false };
    component.slots[2] = { filled: true, userId: 'p3', displayName: 'P3', isMe: false, isGuest: false };
    component.slots[3] = { filled: true, userId: 'p4', displayName: 'P4', isMe: false, isGuest: false };

    matchSvc.checkQuickMatch.and.returnValue(of({
      mode: 'exact',
      circles: [
        { id: 'c-owned', name: 'Mio Circolo', ownerId: 'me-id', lastMatchAt: null },
        { id: 'c-other', name: 'Altro Circolo', ownerId: 'someone-else', lastMatchAt: null },
      ],
    }));

    (component as any).runCheck();
    setTimeout(() => {
      expect(component.checkResult?.circles.map(c => c.id)).toEqual(['c-owned']);
      done();
    }, 0);
  });

  it('does not filter circles when creator is among the 4 slots', (done) => {
    component.slots[1] = { filled: true, userId: 'p2', displayName: 'P2', isMe: false, isGuest: false };
    component.slots[2] = { filled: true, userId: 'p3', displayName: 'P3', isMe: false, isGuest: false };
    component.slots[3] = { filled: true, userId: 'p4', displayName: 'P4', isMe: false, isGuest: false };

    matchSvc.checkQuickMatch.and.returnValue(of({
      mode: 'exact',
      circles: [
        { id: 'c-owned', name: 'Mio Circolo', ownerId: 'me-id', lastMatchAt: null },
        { id: 'c-other', name: 'Altro Circolo', ownerId: 'someone-else', lastMatchAt: null },
      ],
    }));

    (component as any).runCheck();
    setTimeout(() => {
      expect(component.checkResult?.circles.length).toBe(2);
      done();
    }, 0);
  });
});
