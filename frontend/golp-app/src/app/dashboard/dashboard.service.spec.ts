import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { DashboardService } from './dashboard.service';
import { environment } from '../../environments/environment';

describe('DashboardService', () => {
  let service: DashboardService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(DashboardService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('chiama /dashboard/summary senza query param quando circleId è assente (modalità "tutti i circoli")', () => {
    service.getDashboardSummary().subscribe();
    const req = httpMock.expectOne(`${environment.apiUrl}/dashboard/summary`);
    expect(req.request.method).toBe('GET');
    req.flush({ circles: [], activeCircle: null, aggregate: null, urgentMatches: [] });
  });

  it('chiama /dashboard/summary?circleId={id} quando circleId è passato', () => {
    service.getDashboardSummary('c1').subscribe();
    const req = httpMock.expectOne(`${environment.apiUrl}/dashboard/summary?circleId=c1`);
    expect(req.request.method).toBe('GET');
    req.flush({ circles: [], activeCircle: null, aggregate: null, urgentMatches: [] });
  });
});
