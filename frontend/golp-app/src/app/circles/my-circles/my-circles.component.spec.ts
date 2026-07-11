import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { MyCirclesComponent } from './my-circles.component';
import { CircleService, CircleSummary } from '../circle.service';
import { AuthService } from '../../auth/auth.service';

function makeCircle(overrides: Partial<CircleSummary> = {}): CircleSummary {
  return {
    id: 'circle-1',
    name: 'Padel Club Roma',
    sport: 'padel',
    sets: true,
    pointUnit: 'games',
    ownerId: 'owner-1',
    memberCount: 22,
    myRating: 1066,
    myRank: 1,
    ratingMethod: 'Elo',
    ...overrides,
  };
}

describe('MyCirclesComponent', () => {
  let circleSvc: jasmine.SpyObj<CircleService>;
  let authSvc: jasmine.SpyObj<AuthService>;

  beforeEach(async () => {
    circleSvc = jasmine.createSpyObj('CircleService', ['getMyCircles']);
    authSvc   = jasmine.createSpyObj('AuthService', ['getCurrentUserId']);
    authSvc.getCurrentUserId.and.returnValue('owner-1');

    await TestBed.configureTestingModule({
      imports: [MyCirclesComponent],
      providers: [
        provideRouter([]),
        { provide: CircleService, useValue: circleSvc },
        { provide: AuthService, useValue: authSvc },
      ],
    }).compileComponents();
  });

  // US-073: la card deve mostrare etichetta e valore coerenti col metodo di calcolo attivo
  // sul circolo, non sempre "Rating" ELO.
  it('shows "Rating" label for an Elo circle', () => {
    circleSvc.getMyCircles.and.returnValue(of([makeCircle({ ratingMethod: 'Elo', myRating: 1066 })]));
    const fixture = TestBed.createComponent(MyCirclesComponent);
    fixture.detectChanges();

    const label = fixture.nativeElement.querySelector('.rating-label').textContent as string;
    const value = fixture.nativeElement.querySelector('.rating-value').textContent as string;
    expect(label.trim()).toBe('Rating');
    expect(value).toContain('1066');
  });

  it('shows "Punteggio" label for a Game+Bonus circle', () => {
    circleSvc.getMyCircles.and.returnValue(of([makeCircle({ ratingMethod: 'GameBonus', myRating: 42, myRank: 3 })]));
    const fixture = TestBed.createComponent(MyCirclesComponent);
    fixture.detectChanges();

    const label = fixture.nativeElement.querySelector('.rating-label').textContent as string;
    const value = fixture.nativeElement.querySelector('.rating-value').textContent as string;
    const rank = fixture.nativeElement.querySelector('.ranking-value').textContent as string;
    expect(label.trim()).toBe('Punteggio');
    expect(value).toContain('42');
    expect(rank).toContain('3');
  });
});
