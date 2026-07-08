import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { CircleStatsComponent } from './circle-stats.component';
import { CircleService, CircleStatsResponse } from '../circle.service';

const CIRCLE_ID = 'circle-1';

function makeStats(overrides: Partial<CircleStatsResponse> = {}): CircleStatsResponse {
  return {
    bestPartner: null,
    toughestOpponent: null,
    matchesWon: 1,
    matchesLost: 0,
    gamesWon: 0,
    gamesLost: 0,
    recentForm: [],
    ...overrides,
  };
}

describe('CircleStatsComponent', () => {
  let circleSvc: jasmine.SpyObj<CircleService>;

  beforeEach(async () => {
    circleSvc = jasmine.createSpyObj('CircleService', ['getMyStats']);

    await TestBed.configureTestingModule({
      imports: [CircleStatsComponent],
      providers: [
        { provide: CircleService, useValue: circleSvc },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { params: { circleId: CIRCLE_ID } } },
        },
      ],
    }).compileComponents();
  });

  it('shows best partner name and 75% win rate', () => {
    circleSvc.getMyStats.and.returnValue(of(makeStats({
      bestPartner: { userId: 'u1', name: 'Marco Rossi', winRate: 0.75, gamesTogether: 8 },
    })));

    const fixture = TestBed.createComponent(CircleStatsComponent);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;

    expect(el.textContent).toContain('Marco Rossi');
    expect(el.textContent).toContain('75%');
    expect(el.textContent).toContain('8 partite insieme');
  });

  it('shows "Dati non sufficienti" for best partner when null', () => {
    circleSvc.getMyStats.and.returnValue(of(makeStats({
      toughestOpponent: { userId: 'u2', name: 'Luigi Bianchi', winRate: 0.20, gamesAgainst: 5 },
    })));

    const fixture = TestBed.createComponent(CircleStatsComponent);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;

    expect(el.textContent).toContain('Dati non sufficienti');
    expect(el.textContent).toContain('≥3 partite con lo stesso compagno');
  });

  it('shows toughest opponent name and 20% win rate', () => {
    circleSvc.getMyStats.and.returnValue(of(makeStats({
      toughestOpponent: { userId: 'u2', name: 'Luigi Bianchi', winRate: 0.20, gamesAgainst: 5 },
    })));

    const fixture = TestBed.createComponent(CircleStatsComponent);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;

    expect(el.textContent).toContain('Luigi Bianchi');
    expect(el.textContent).toContain('20%');
    expect(el.textContent).toContain('5 partite contro');
  });

  it('shows empty state message when no matches played', () => {
    circleSvc.getMyStats.and.returnValue(of(makeStats({ matchesWon: 0, matchesLost: 0 })));

    const fixture = TestBed.createComponent(CircleStatsComponent);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;

    expect(el.textContent).toContain('Ancora nessuna statistica');
    expect(el.querySelector('.stats-list')).toBeNull();
  });

  it('shows matches and games record, and recent form badges', () => {
    circleSvc.getMyStats.and.returnValue(of(makeStats({
      matchesWon: 7, matchesLost: 3, gamesWon: 45, gamesLost: 30,
      recentForm: ['W', 'L', 'W', 'W', 'L'],
    })));

    const fixture = TestBed.createComponent(CircleStatsComponent);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;

    expect(el.textContent).toContain('7');
    expect(el.textContent).toContain('3');
    expect(el.textContent).toContain('45');
    expect(el.textContent).toContain('30');
    expect(el.querySelectorAll('.stat-form-badge').length).toBe(5);
    expect(el.querySelectorAll('.stat-form-badge--win').length).toBe(3);
    expect(el.querySelectorAll('.stat-form-badge--loss').length).toBe(2);
  });

  it('hides recent form block when no matches in the trend', () => {
    circleSvc.getMyStats.and.returnValue(of(makeStats({ recentForm: [] })));

    const fixture = TestBed.createComponent(CircleStatsComponent);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;

    expect(el.querySelector('.stat-card--form')).toBeNull();
  });

  it('shows loading state while request is pending', () => {
    const subject = new Subject<CircleStatsResponse>();
    circleSvc.getMyStats.and.returnValue(subject.asObservable());

    const fixture = TestBed.createComponent(CircleStatsComponent);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;

    expect(el.textContent).toContain('Caricamento');
    expect(el.querySelector('.stats-list')).toBeNull();
  });

  it('shows error message on API failure', () => {
    circleSvc.getMyStats.and.returnValue(throwError(() => new Error('network')));

    const fixture = TestBed.createComponent(CircleStatsComponent);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;

    expect(el.textContent).toContain('Impossibile caricare le statistiche');
  });
});
