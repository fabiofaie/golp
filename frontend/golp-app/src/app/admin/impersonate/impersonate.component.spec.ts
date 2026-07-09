import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ImpersonateComponent } from './impersonate.component';
import { AuthService } from '../../auth/auth.service';

describe('ImpersonateComponent (US-059)', () => {
  let component: ImpersonateComponent;
  let authSvc: jasmine.SpyObj<AuthService>;
  let router: Router;

  beforeEach(async () => {
    authSvc = jasmine.createSpyObj('AuthService', ['startImpersonation']);

    await TestBed.configureTestingModule({
      imports: [ImpersonateComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authSvc },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(ImpersonateComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    spyOn(router, 'navigate');
  });

  it('form invalido → non chiama startImpersonation', () => {
    component.form.setValue({ email: 'not-an-email' });
    component.submit();

    expect(authSvc.startImpersonation).not.toHaveBeenCalled();
  });

  it('successo → naviga a /dashboard', () => {
    authSvc.startImpersonation.and.returnValue(of({ accessToken: 'a', refreshToken: 'r' }));
    component.form.setValue({ email: 'target@b.com' });

    component.submit();

    expect(authSvc.startImpersonation).toHaveBeenCalledWith('target@b.com');
    expect(router.navigate).toHaveBeenCalledWith(['/dashboard']);
  });

  it('404 → mostra errore "utente non trovato"', () => {
    authSvc.startImpersonation.and.returnValue(throwError(() => ({ status: 404 })));
    component.form.setValue({ email: 'nobody@b.com' });

    component.submit();

    expect(component.error).toContain('Nessun utente trovato');
    expect(router.navigate).not.toHaveBeenCalled();
  });
});
