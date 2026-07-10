import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { ActiveCircleService } from './active-circle.service';
import { CircleService, CircleSummary } from '../circles/circle.service';

const CIRCLE_OK: CircleSummary = {
  id: 'c1', name: 'Padel Club', sport: 'padel', sets: true, pointUnit: 'games',
  ownerId: 'owner', memberCount: 4, myRating: 1000, myRank: 1, joinedAt: '2026-01-01T00:00:00Z',
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
  it('sceglie il circolo attivo tramite pickActiveCircle', async () => {
    const { service } = await setup([CIRCLE_OK]);
    expect(service.activeCircle()?.id).toBe('c1');
  });

  it('carica i circoli una sola volta anche con più chiamate a ensureLoaded', async () => {
    const { service, circleServiceMock } = await setup([CIRCLE_OK]);
    service.ensureLoaded();
    service.ensureLoaded();
    expect(circleServiceMock.getMyCircles).toHaveBeenCalledTimes(1);
  });

  it('onRecordMatchClick naviga sempre a Quick Match, indipendentemente dal circolo attivo', async () => {
    const { service } = await setup([CIRCLE_OK]);
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigate');

    service.onRecordMatchClick();

    expect(navigateSpy).toHaveBeenCalledWith(['/match/quick']);
  });

  it('onRecordMatchClick naviga a Quick Match anche senza circoli', async () => {
    const { service } = await setup([]);
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigate');

    service.onRecordMatchClick();

    expect(navigateSpy).toHaveBeenCalledWith(['/match/quick']);
  });
});
