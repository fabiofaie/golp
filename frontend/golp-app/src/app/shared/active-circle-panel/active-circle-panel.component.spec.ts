import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ActiveCirclePanelComponent } from './active-circle-panel.component';
import { ActiveCircleService } from '../active-circle.service';
import { CircleService, CircleSummary } from '../../circles/circle.service';

function makeCircle(overrides: Partial<CircleSummary>): CircleSummary {
  return {
    id: 'x', name: 'Circolo', sport: 'padel', sets: true, pointUnit: 'games',
    ownerId: 'owner', memberCount: 4, myRating: 1000, myRank: 1, joinedAt: '2026-01-01T00:00:00Z',
    ...overrides,
  };
}

const FEW_CIRCLES: CircleSummary[] = [
  makeCircle({ id: 'c1', name: 'Padel Amici' }),
  makeCircle({ id: 'c2', name: 'Beach Tennis Estate', sport: 'beach-tennis' }),
];

const MANY_CIRCLES: CircleSummary[] = Array.from({ length: 7 }, (_, i) =>
  makeCircle({ id: `c${i}`, name: `Circolo ${i}` })
);

async function setup(circles: CircleSummary[]) {
  localStorage.removeItem('golp_active_circle_id');
  localStorage.removeItem('golp_favorite_circle_ids');

  const circleServiceMock = {
    getMyCircles: jasmine.createSpy('getMyCircles').and.returnValue(of(circles)),
  };
  await TestBed.configureTestingModule({
    imports: [ActiveCirclePanelComponent],
    providers: [
      provideRouter([]),
      { provide: CircleService, useValue: circleServiceMock },
    ],
  }).compileComponents();

  const activeCircleService = TestBed.inject(ActiveCircleService);
  activeCircleService.ensureLoaded();

  const fixture: ComponentFixture<ActiveCirclePanelComponent> = TestBed.createComponent(ActiveCirclePanelComponent);
  fixture.componentRef.setInput('open', true);
  fixture.detectChanges();
  return { fixture, activeCircleService };
}

describe('ActiveCirclePanelComponent', () => {
  it('mostra "Tutti i circoli" più una riga per ciascun circolo', async () => {
    const { fixture } = await setup(FEW_CIRCLES);
    const rows = fixture.nativeElement.querySelectorAll('.circle-row');
    expect(rows.length).toBe(3); // "Tutti i circoli" + 2 circoli
  });

  it('click su una riga seleziona il circolo e chiude il pannello', async () => {
    const { fixture, activeCircleService } = await setup(FEW_CIRCLES);
    const closedSpy = jasmine.createSpy('closed');
    fixture.componentInstance.closed.subscribe(closedSpy);

    const rows: NodeListOf<HTMLButtonElement> = fixture.nativeElement.querySelectorAll('.circle-row');
    rows[1].click(); // primo circolo dopo "Tutti i circoli"

    expect(activeCircleService.activeSelection()).toBe('c1');
    expect(closedSpy).toHaveBeenCalled();
  });

  it('non mostra il campo di ricerca con pochi circoli', async () => {
    const { fixture } = await setup(FEW_CIRCLES);
    expect(fixture.nativeElement.querySelector('.sheet-search')).toBeNull();
  });

  it('mostra il campo di ricerca con più di 5 circoli e filtra per nome', async () => {
    const { fixture } = await setup(MANY_CIRCLES);
    const input: HTMLInputElement = fixture.nativeElement.querySelector('.sheet-search input');
    expect(input).not.toBeNull();

    input.value = 'Circolo 3';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('.circle-row');
    // "Tutti i circoli" + solo "Circolo 3"
    expect(rows.length).toBe(2);
  });

  it('click sulla stella aggiunge ai preferiti e riordina in gruppo "Preferiti"', async () => {
    const { fixture, activeCircleService } = await setup(MANY_CIRCLES);
    const firstStar: HTMLButtonElement = fixture.nativeElement.querySelector('.fav-star');
    firstStar.click();
    fixture.detectChanges();

    expect(activeCircleService.favoriteCircleIds().has('c0')).toBeTrue();
    const groupLabels = fixture.nativeElement.querySelectorAll('.sheet-group-label');
    expect(groupLabels.length).toBe(2); // "Preferiti" + "Altri circoli"
    expect(groupLabels[0].textContent).toContain('Preferiti');
  });
});
