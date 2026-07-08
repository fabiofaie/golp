import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { CircleRatingConfigComponent } from './circle-rating-config.component';
import { CircleService, UpdateRatingConfigResult } from '../circle.service';

const CIRCLE_ID = 'circle-1';
const CIRCLE_NAME = 'Test Circle';

describe('CircleRatingConfigComponent', () => {
  let circleSvc: jasmine.SpyObj<CircleService>;

  beforeEach(async () => {
    circleSvc = jasmine.createSpyObj('CircleService', ['updateRatingConfig']);

    await TestBed.configureTestingModule({
      imports: [CircleRatingConfigComponent],
      providers: [{ provide: CircleService, useValue: circleSvc }],
    }).compileComponents();
  });

  function createComponent(overrides: Partial<CircleRatingConfigComponent> = {}) {
    const fixture = TestBed.createComponent(CircleRatingConfigComponent);
    fixture.componentInstance.circleId = CIRCLE_ID;
    fixture.componentInstance.circleName = CIRCLE_NAME;
    Object.assign(fixture.componentInstance, overrides);
    fixture.detectChanges();
    return fixture;
  }

  it('inizializza selectedMethod da ratingMethod in input (ELO default)', () => {
    const fixture = createComponent({ ratingMethod: 'Elo' });
    expect(fixture.componentInstance.selectedMethod).toBe('Elo');
  });

  it('select("GameBonus") cambia il metodo selezionato', () => {
    const fixture = createComponent();
    const comp = fixture.componentInstance;

    comp.select('GameBonus');

    expect(comp.selectedMethod).toBe('GameBonus');
  });

  it('la sezione parametri finestra è visibile solo quando selectedMethod = GameBonus', () => {
    const fixture = createComponent();
    const comp = fixture.componentInstance;

    comp.select('Elo');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.window-params')).toBeNull();

    comp.select('GameBonus');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.window-params')).not.toBeNull();
  });

  it('save() chiama updateRatingConfig con metodo e parametri correnti', () => {
    const result: UpdateRatingConfigResult = { ratingMethod: 'GameBonus', gameBonusWindowMatches: 20, gameBonusWindowWeeks: 4 };
    circleSvc.updateRatingConfig.and.returnValue(of(result));
    const fixture = createComponent();
    const comp = fixture.componentInstance;
    comp.select('GameBonus');
    comp.windowMatches = 20;
    comp.windowWeeks = 4;

    comp.save();

    expect(circleSvc.updateRatingConfig).toHaveBeenCalledWith(CIRCLE_ID, 'GameBonus', 20, 4);
    expect(comp.saved_).toBeTrue();
    expect(comp.saving).toBeFalse();
  });

  it('save() emette saved con il risultato', () => {
    const result: UpdateRatingConfigResult = { ratingMethod: 'Elo', gameBonusWindowMatches: 30, gameBonusWindowWeeks: 6 };
    circleSvc.updateRatingConfig.and.returnValue(of(result));
    const fixture = createComponent();
    const comp = fixture.componentInstance;
    let emitted: UpdateRatingConfigResult | undefined;
    comp.saved.subscribe((r) => (emitted = r));

    comp.save();

    expect(emitted).toEqual(result);
  });

  it('save() con errore mostra messaggio ed esce da saving', () => {
    circleSvc.updateRatingConfig.and.returnValue(throwError(() => ({ status: 403 })));
    const fixture = createComponent();
    const comp = fixture.componentInstance;

    comp.save();

    expect(comp.error).toContain('Impossibile');
    expect(comp.saving).toBeFalse();
  });

  it('close emette closed event', () => {
    const fixture = createComponent();
    const comp = fixture.componentInstance;
    let emitted = false;
    comp.closed.subscribe(() => (emitted = true));

    comp.close();

    expect(emitted).toBeTrue();
  });
});
