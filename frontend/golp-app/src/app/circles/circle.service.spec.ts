import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { CircleService, UpdateRatingConfigResult } from './circle.service';
import { environment } from '../../environments/environment';

describe('CircleService — updateRatingConfig (US-051)', () => {
  let service: CircleService;
  let http: HttpTestingController;
  const circleId = 'circle-1';
  const url = `${environment.apiUrl}/circles/${circleId}/rating-config`;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule] });
    service = TestBed.inject(CircleService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('invia PUT con ratingMethod e parametri finestra', async () => {
    const mockResult: UpdateRatingConfigResult = {
      ratingMethod: 'GameBonus',
      gameBonusWindowMatches: 20,
      gameBonusWindowWeeks: 4,
    };

    const promise = new Promise((resolve) => service.updateRatingConfig(circleId, 'GameBonus', 20, 4).subscribe(resolve));
    const req = http.expectOne(url);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ ratingMethod: 'GameBonus', gameBonusWindowMatches: 20, gameBonusWindowWeeks: 4 });
    req.flush(mockResult);

    expect(await promise).toEqual(mockResult);
  });

  it('invia PUT senza parametri finestra quando si passa a ELO', async () => {
    const mockResult: UpdateRatingConfigResult = { ratingMethod: 'Elo', gameBonusWindowMatches: 30, gameBonusWindowWeeks: 6 };

    const promise = new Promise((resolve) => service.updateRatingConfig(circleId, 'Elo').subscribe(resolve));
    const req = http.expectOne(url);
    expect(req.request.body).toEqual({ ratingMethod: 'Elo', gameBonusWindowMatches: undefined, gameBonusWindowWeeks: undefined });
    req.flush(mockResult);

    expect(await promise).toEqual(mockResult);
  });

  it('propaga errore 403 (non-owner)', async () => {
    let error: unknown;
    const promise = new Promise<void>((resolve) => {
      service.updateRatingConfig(circleId, 'GameBonus').subscribe({
        error: (err) => { error = err; resolve(); },
      });
    });

    http.expectOne(url).flush({ error: 'Solo il proprietario' }, { status: 403, statusText: 'Forbidden' });
    await promise;

    expect((error as { status: number }).status).toBe(403);
  });

  it('propaga errore 400 (ratingMethod non valido)', async () => {
    let error: unknown;
    const promise = new Promise<void>((resolve) => {
      service.updateRatingConfig(circleId, 'Bogus' as 'Elo').subscribe({
        error: (err) => { error = err; resolve(); },
      });
    });

    http.expectOne(url).flush({ error: 'ratingMethod non valido' }, { status: 400, statusText: 'Bad Request' });
    await promise;

    expect((error as { status: number }).status).toBe(400);
  });
});
