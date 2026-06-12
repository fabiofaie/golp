import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { CircleAwardsComponent } from './circle-awards.component';
import { CircleService, CircleAwardsResponse } from '../circle.service';

const CIRCLE_ID = 'circle-1';

function makeAwards(overrides: Partial<CircleAwardsResponse> = {}): CircleAwardsResponse {
  return {
    currentMonth: { period: '2026-06', winner: null },
    currentYear:  { period: '2026',    winner: null },
    ...overrides,
  };
}

describe('CircleAwardsComponent', () => {
  let circleSvc: jasmine.SpyObj<CircleService>;

  beforeEach(async () => {
    circleSvc = jasmine.createSpyObj('CircleService', ['getAwards']);

    await TestBed.configureTestingModule({
      imports: [CircleAwardsComponent],
      providers: [
        { provide: CircleService, useValue: circleSvc },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { params: { circleId: CIRCLE_ID } } },
        },
      ],
    }).compileComponents();
  });

  it('shows winner name and +42 pt when month winner is present', () => {
    circleSvc.getAwards.and.returnValue(of(makeAwards({
      currentMonth: {
        period: '2026-06',
        winner: { userId: 'u1', name: 'Marco Rossi', netGain: 42, matchesPlayed: 5 },
      },
    })));

    const fixture = TestBed.createComponent(CircleAwardsComponent);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;

    expect(el.textContent).toContain('Marco Rossi');
    expect(el.textContent).toContain('+42 pt guadagnati');
  });

  it('shows "Nessun premiato ancora" when year winner is null', () => {
    circleSvc.getAwards.and.returnValue(of(makeAwards()));

    const fixture = TestBed.createComponent(CircleAwardsComponent);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;

    const emptySlots = el.querySelectorAll('.award-empty');
    expect(emptySlots.length).toBe(2);
    expect(emptySlots[0].textContent).toContain('Nessun premiato ancora');
  });

  it('shows loading state while request is pending', () => {
    const subject = new Subject<CircleAwardsResponse>();
    circleSvc.getAwards.and.returnValue(subject.asObservable());

    const fixture = TestBed.createComponent(CircleAwardsComponent);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;

    expect(el.textContent).toContain('Caricamento');
    expect(el.querySelector('.award-card')).toBeNull();
  });

  it('shows error message on API failure', () => {
    circleSvc.getAwards.and.returnValue(throwError(() => new Error('network')));

    const fixture = TestBed.createComponent(CircleAwardsComponent);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;

    expect(el.textContent).toContain('Impossibile caricare i premi');
  });
});
