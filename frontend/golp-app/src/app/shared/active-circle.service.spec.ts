import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { ActiveCircleService } from './active-circle.service';
import { CircleService, CircleSummary } from '../circles/circle.service';

const CIRCLE_OK: CircleSummary = {
  id: 'c1', name: 'Padel Club', sport: 'padel', sets: true, pointUnit: 'games',
  ownerId: 'owner', memberCount: 4, myRating: 1000, myRank: 1, joinedAt: '2026-01-01T00:00:00Z',
};

const CIRCLE_OK_2: CircleSummary = {
  id: 'c2', name: 'Beach Tennis', sport: 'beach-tennis', sets: true, pointUnit: 'games',
  ownerId: 'owner', memberCount: 6, myRating: 1100, myRank: 2, joinedAt: '2026-02-01T00:00:00Z',
};

async function setup(circles: CircleSummary[]) {
  const circleServiceMock = {
    getMyCircles: jasmine.createSpy('getMyCircles').and.returnValue(of(circles)),
  };
  await TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: CircleService, useValue: circleServiceMock },
    ],
  }).compileComponents();
  const service = TestBed.inject(ActiveCircleService);
  service.ensureLoaded();
  return { service, circleServiceMock };
}

describe('ActiveCircleService', () => {
  beforeEach(() => {
    localStorage.removeItem('golp_active_circle_id');
    localStorage.removeItem('golp_favorite_circle_ids');
  });

  it('sceglie il circolo attivo tramite pickActiveCircle quando non c\'è selezione salvata', async () => {
    const { service } = await setup([CIRCLE_OK]);
    expect(service.activeCircle()?.id).toBe('c1');
  });

  it('carica i circoli una sola volta anche con più chiamate a ensureLoaded', async () => {
    const { service, circleServiceMock } = await setup([CIRCLE_OK]);
    service.ensureLoaded();
    service.ensureLoaded();
    expect(circleServiceMock.getMyCircles).toHaveBeenCalledTimes(1);
  });

  it('onRecordMatchClick naviga a Quick Match con il circleId attivo come default', async () => {
    const { service } = await setup([CIRCLE_OK]);
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigate');

    service.onRecordMatchClick();

    expect(navigateSpy).toHaveBeenCalledWith(['/match/quick'], { queryParams: { circleId: 'c1' } });
  });

  it('onRecordMatchClick naviga a Quick Match senza query param quando non ci sono circoli', async () => {
    const { service } = await setup([]);
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigate');

    service.onRecordMatchClick();

    expect(navigateSpy).toHaveBeenCalledWith(['/match/quick']);
  });

  it('selectCircle imposta il circolo attivo esplicito e ha priorità sul fallback', async () => {
    const { service } = await setup([CIRCLE_OK, CIRCLE_OK_2]);
    // fallback sceglierebbe c1 (più vecchio), selezione esplicita sceglie c2
    service.selectCircle('c2');
    expect(service.activeCircle()?.id).toBe('c2');
  });

  it('selectCircle persiste la selezione tra istanze del service (localStorage)', async () => {
    const { service } = await setup([CIRCLE_OK, CIRCLE_OK_2]);
    service.selectCircle('c2');
    expect(localStorage.getItem('golp_active_circle_id')).toBe('c2');
  });

  it('selezione "tutti i circoli" rende activeCircle null e isAllCirclesSelected true', async () => {
    const { service } = await setup([CIRCLE_OK, CIRCLE_OK_2]);
    service.selectCircle('all');
    expect(service.activeCircle()).toBeNull();
    expect(service.isAllCirclesSelected()).toBeTrue();
  });

  it('se il circleId salvato non è più tra i circoli utente, ricade su pickActiveCircle', async () => {
    localStorage.setItem('golp_active_circle_id', 'circolo-non-esistente');
    const { service } = await setup([CIRCLE_OK]);
    expect(service.activeCircle()?.id).toBe('c1');
  });

  it('toggleFavorite aggiunge e rimuove un circolo dai preferiti, persistendo', async () => {
    const { service } = await setup([CIRCLE_OK]);
    service.toggleFavorite('c1');
    expect(service.favoriteCircleIds().has('c1')).toBeTrue();
    expect(JSON.parse(localStorage.getItem('golp_favorite_circle_ids') ?? '[]')).toEqual(['c1']);

    service.toggleFavorite('c1');
    expect(service.favoriteCircleIds().has('c1')).toBeFalse();
  });

  it('onRecordMatchClick naviga senza circleId quando "tutti i circoli" è selezionato (US-067)', async () => {
    const { service } = await setup([CIRCLE_OK, CIRCLE_OK_2]);
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigate');

    service.selectCircle('all');
    service.onRecordMatchClick();

    expect(navigateSpy).toHaveBeenCalledWith(['/match/quick']);
  });

  it('reset azzera selezione, preferiti e localStorage', async () => {
    const { service } = await setup([CIRCLE_OK]);
    service.selectCircle('c1');
    service.toggleFavorite('c1');

    service.reset();

    expect(service.activeSelection()).toBeNull();
    expect(service.favoriteCircleIds().size).toBe(0);
    expect(localStorage.getItem('golp_active_circle_id')).toBeNull();
    expect(localStorage.getItem('golp_favorite_circle_ids')).toBeNull();
  });
});
