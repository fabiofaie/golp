import { TestBed } from '@angular/core/testing';
import { Location } from '@angular/common';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { GameBonusInfoComponent } from './game-bonus-info.component';
import { GameBonusInfoService, SimulateGameBonusResponse } from './game-bonus-info.service';

describe('GameBonusInfoComponent', () => {
  let gbService: jasmine.SpyObj<GameBonusInfoService>;
  let location: jasmine.SpyObj<Location>;

  beforeEach(async () => {
    gbService = jasmine.createSpyObj('GameBonusInfoService', ['simulate']);
    location = jasmine.createSpyObj('Location', ['back']);

    await TestBed.configureTestingModule({
      imports: [GameBonusInfoComponent],
      providers: [
        { provide: GameBonusInfoService, useValue: gbService },
        { provide: Location, useValue: location },
        provideRouter([]),
      ],
    }).compileComponents();
  });

  function create() {
    const fixture = TestBed.createComponent(GameBonusInfoComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('should create', () => {
    const fixture = create();
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('submit with unico mode sends sets without a client-computed winner flag', () => {
    const fixture = create();
    const comp = fixture.componentInstance;
    const response: SimulateGameBonusResponse = { team1Points: 3, team2Points: 0 };
    gbService.simulate.and.returnValue(of(response));

    comp.form.patchValue({ myScore: 6, oppScore: 4, team1CurrentScore: 0, team2CurrentScore: 0 });
    comp.submit();

    const payload = gbService.simulate.calls.mostRecent().args[0];
    expect(payload.sets).toEqual([{ team1Score: 6, team2Score: 4 }]);
    expect((payload as any).team1Won).toBeUndefined();
    expect(comp.result()).toEqual(response);
  });

  it('submit blocked when sets won are tied', () => {
    const fixture = create();
    const comp = fixture.componentInstance;

    comp.form.patchValue({ myScore: 6, oppScore: 6, team1CurrentScore: 0, team2CurrentScore: 0 });
    comp.submit();

    expect(gbService.simulate).not.toHaveBeenCalled();
    expect(comp.errorMsg()).toContain('vincente');
  });

  it('submit in "per set" mode lets the backend determine the winner from sets won, not total games (bug US-056)', () => {
    const fixture = create();
    const comp = fixture.componentInstance;
    const response: SimulateGameBonusResponse = { team1Points: 5, team2Points: 0 };
    gbService.simulate.and.returnValue(of(response));

    comp.setMode('set');
    comp.sets.at(0).patchValue({ myScore: 6, oppScore: 4 });
    comp.addSet();
    comp.sets.at(1).patchValue({ myScore: 6, oppScore: 4 });
    comp.addSet();
    comp.sets.at(2).patchValue({ myScore: 1, oppScore: 6 });
    comp.form.patchValue({ team1CurrentScore: 0, team2CurrentScore: 0 });
    comp.submit();

    // 13 game totali contro 16 (sfavorevole), ma 2 set vinti su 3: il payload manda solo i set,
    // il calcolo del vincitore è responsabilità del backend (nessun team1Won inviato dal client).
    const payload = gbService.simulate.calls.mostRecent().args[0];
    expect(payload.sets).toEqual([
      { team1Score: 6, team2Score: 4 },
      { team1Score: 6, team2Score: 4 },
      { team1Score: 1, team2Score: 6 },
    ]);
    expect((payload as any).team1Won).toBeUndefined();
    expect(comp.result()).toEqual(response);
  });

  it('submit blocked when all scores are zero', () => {
    const fixture = create();
    const comp = fixture.componentInstance;

    comp.form.patchValue({ myScore: 0, oppScore: 0, team1CurrentScore: 0, team2CurrentScore: 0 });
    comp.submit();

    expect(gbService.simulate).not.toHaveBeenCalled();
    expect(comp.errorMsg()).toContain('maggiore di 0');
  });

  it('submit shows error message on service failure', () => {
    const fixture = create();
    const comp = fixture.componentInstance;
    gbService.simulate.and.returnValue(throwError(() => new Error('net')));

    comp.form.patchValue({ myScore: 6, oppScore: 4, team1CurrentScore: 0, team2CurrentScore: 0 });
    comp.submit();

    expect(comp.errorMsg()).toContain('Errore');
  });

  it('goBack calls location.back', () => {
    const fixture = create();
    fixture.componentInstance.goBack();
    expect(location.back).toHaveBeenCalled();
  });
});
