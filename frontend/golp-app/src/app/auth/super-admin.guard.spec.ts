import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { superAdminGuard } from './super-admin.guard';
import { AuthService } from './auth.service';

describe('superAdminGuard (US-059)', () => {
  let authMock: jasmine.SpyObj<AuthService>;
  let router: Router;

  beforeEach(() => {
    authMock = jasmine.createSpyObj('AuthService', ['isAuthenticated', 'isSuperAdmin']);

    TestBed.configureTestingModule({
      providers: [{ provide: AuthService, useValue: authMock }],
    });
    router = TestBed.inject(Router);
  });

  const runGuard = () =>
    TestBed.runInInjectionContext(() => superAdminGuard({} as never, {} as never));

  it('consente accesso se autenticato e super admin', () => {
    authMock.isAuthenticated.and.returnValue(true);
    authMock.isSuperAdmin.and.returnValue(true);

    expect(runGuard()).toBeTrue();
  });

  it('reindirizza a /dashboard se non super admin', () => {
    authMock.isAuthenticated.and.returnValue(true);
    authMock.isSuperAdmin.and.returnValue(false);

    const result = runGuard() as UrlTree;

    expect(result instanceof UrlTree).toBeTrue();
    expect(router.serializeUrl(result)).toBe('/dashboard');
  });
});
