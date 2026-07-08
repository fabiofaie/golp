import { TestBed } from '@angular/core/testing';
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
});
