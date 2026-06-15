import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { ActivatedRoute } from '@angular/router';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { JoinCircleComponent } from './join-circle.component';
import { AuthService } from '../../auth/auth.service';
import { CircleService, JoinByTokenResult } from '../circle.service';

const TOKEN = 'abc123token';
const CIRCLE_ID = 'circle-uuid-1';

function makeResult(overrides: Partial<JoinByTokenResult> = {}): JoinByTokenResult {
  return { circleId: CIRCLE_ID, myRating: 1000, alreadyMember: false, ...overrides };
}

describe('JoinCircleComponent', () => {
  let routerSpy: jasmine.SpyObj<Router>;
  let authSvc: jasmine.SpyObj<AuthService>;
  let circleSvc: jasmine.SpyObj<CircleService>;

  function setup(token: string | null, authenticated: boolean) {
    routerSpy = jasmine.createSpyObj('Router', ['navigate', 'createUrlTree', 'serializeUrl']);
    routerSpy.createUrlTree.and.returnValue({} as any);
    routerSpy.serializeUrl.and.returnValue('');
    authSvc = jasmine.createSpyObj('AuthService', ['isAuthenticated']);
    authSvc.isAuthenticated.and.returnValue(authenticated);
    circleSvc = jasmine.createSpyObj('CircleService', ['joinByToken']);

    TestBed.configureTestingModule({
      imports: [JoinCircleComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authSvc },
        { provide: CircleService, useValue: circleSvc },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: {
                get: (key: string) => key === 'token' ? token : null,
              },
            },
          },
        },
      ],
    });
  }

  it('shows error when token is missing', () => {
    setup(null, false);
    const fixture = TestBed.createComponent(JoinCircleComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    expect(comp.error).toBe('Link non valido o scaduto');
  });

  it('unauthenticated user with token — shows register/login CTAs, no redirect', () => {
    setup(TOKEN, false);
    const fixture = TestBed.createComponent(JoinCircleComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    expect(comp.error).toBe('');
    expect(circleSvc.joinByToken).not.toHaveBeenCalled();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).toContain('Registrati');
  });

  it('authenticated user with valid token — joins and navigates to circle', () => {
    setup(TOKEN, true);
    circleSvc.joinByToken.and.returnValue(of(makeResult()));
    const fixture = TestBed.createComponent(JoinCircleComponent);
    const router = TestBed.inject(Router);
    spyOn(router, 'navigate');
    fixture.detectChanges();
    expect(circleSvc.joinByToken).toHaveBeenCalledWith(TOKEN);
    expect(router.navigate).toHaveBeenCalledWith(['/circles']);
  });

  it('authenticated user with invalid token — shows error', () => {
    setup(TOKEN, true);
    circleSvc.joinByToken.and.returnValue(throwError(() => ({ status: 404 })));
    const fixture = TestBed.createComponent(JoinCircleComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    expect(comp.error).toBe('Link non valido o scaduto');
  });
});
