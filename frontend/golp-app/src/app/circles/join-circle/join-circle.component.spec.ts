import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { ActivatedRoute } from '@angular/router';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { JoinCircleComponent } from './join-circle.component';
import { AuthService } from '../../auth/auth.service';
import { CircleService, InviteInfo, JoinByTokenResult } from '../circle.service';

const TOKEN = 'abc123token';
const CIRCLE_ID = 'circle-uuid-1';
const CIRCLE_NAME = 'Padel Club Roma';

function makeResult(overrides: Partial<JoinByTokenResult> = {}): JoinByTokenResult {
  return { circleId: CIRCLE_ID, myRating: 1000, alreadyMember: false, ...overrides };
}

function makeInviteInfo(overrides: Partial<InviteInfo> = {}): InviteInfo {
  return { valid: true, circleName: CIRCLE_NAME, ...overrides };
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
    circleSvc = jasmine.createSpyObj('CircleService', ['joinByToken', 'getInviteInfo']);
    circleSvc.getInviteInfo.and.returnValue(of(makeInviteInfo()));

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
    expect(circleSvc.getInviteInfo).not.toHaveBeenCalled();
  });

  it('invalid token — shows error without asking the question', () => {
    setup(TOKEN, false);
    circleSvc.getInviteInfo.and.returnValue(throwError(() => ({ status: 404 })));
    const fixture = TestBed.createComponent(JoinCircleComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    expect(comp.error).toBe('Link non valido o scaduto');
    expect(comp.hasUsedGolp).toBeNull();
  });

  it('unauthenticated user with valid token — shows the "hai già usato GOLP?" question', () => {
    setup(TOKEN, false);
    const fixture = TestBed.createComponent(JoinCircleComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    expect(comp.error).toBe('');
    expect(comp.hasUsedGolp).toBeNull();
    expect(circleSvc.joinByToken).not.toHaveBeenCalled();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).toContain('Hai già usato');
  });

  it('answering "no" shows the register CTA with inviteToken in query params', () => {
    setup(TOKEN, false);
    const fixture = TestBed.createComponent(JoinCircleComponent);
    fixture.detectChanges();
    fixture.componentInstance.answerHasUsedGolp(false);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).toContain('Registrati');
    expect(fixture.componentInstance.inviteTokenParam).toEqual({ inviteToken: TOKEN });
  });

  it('answering "sì" shows the login CTA with inviteToken in query params', () => {
    setup(TOKEN, false);
    const fixture = TestBed.createComponent(JoinCircleComponent);
    fixture.detectChanges();
    fixture.componentInstance.answerHasUsedGolp(true);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).toContain('Accedi');
    expect(fixture.componentInstance.inviteTokenParam).toEqual({ inviteToken: TOKEN });
  });

  it('authenticated user with valid token — skips the question, joins and navigates to circle', () => {
    setup(TOKEN, true);
    circleSvc.joinByToken.and.returnValue(of(makeResult()));
    const fixture = TestBed.createComponent(JoinCircleComponent);
    const router = TestBed.inject(Router);
    spyOn(router, 'navigate');
    fixture.detectChanges();
    expect(circleSvc.joinByToken).toHaveBeenCalledWith(TOKEN);
    expect(router.navigate).toHaveBeenCalledWith(['/circles']);
  });

  it('authenticated user — join failure shows error', () => {
    setup(TOKEN, true);
    circleSvc.joinByToken.and.returnValue(throwError(() => ({ status: 404 })));
    const fixture = TestBed.createComponent(JoinCircleComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    expect(comp.error).toBe('Link non valido o scaduto');
  });
});
