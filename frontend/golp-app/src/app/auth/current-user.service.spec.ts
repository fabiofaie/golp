import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { CurrentUserService, CurrentUser } from './current-user.service';
import { environment } from '../../environments/environment';

describe('CurrentUserService', () => {
  let service: CurrentUserService;
  let http: HttpTestingController;
  const apiUrl = `${environment.apiUrl}/auth/me`;

  const mockUser: CurrentUser = { id: 'abc', name: 'Mario', email: 'mario@test.com' };

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule] });
    service = TestBed.inject(CurrentUserService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('load() popola il signal', async () => {
    const promise = service.load();
    http.expectOne(apiUrl).flush(mockUser);
    await promise;
    expect(service.currentUser()).toEqual(mockUser);
  });

  it('load() con errore HTTP lascia signal null', async () => {
    const promise = service.load();
    http.expectOne(apiUrl).error(new ProgressEvent('error'));
    await promise;
    expect(service.currentUser()).toBeNull();
  });

  it('updateName() aggiorna il signal col nuovo valore', async () => {
    const updated: CurrentUser = { ...mockUser, name: 'Luigi' };
    const promise = service.updateName('Luigi');
    const req = http.expectOne(apiUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ name: 'Luigi' });
    req.flush(updated);
    await promise;
    expect(service.currentUser()).toEqual(updated);
  });

  it('clear() azzera il signal', async () => {
    const loadPromise = service.load();
    http.expectOne(apiUrl).flush(mockUser);
    await loadPromise;
    service.clear();
    expect(service.currentUser()).toBeNull();
  });
});
