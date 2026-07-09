import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { ImpersonationBannerComponent } from './impersonation-banner.component';
import { AuthService } from '../../auth/auth.service';

describe('ImpersonationBannerComponent (US-059/060)', () => {
  let authSvc: jasmine.SpyObj<AuthService>;
  let router: Router;

  const setup = async () => {
    await TestBed.configureTestingModule({
      imports: [ImpersonationBannerComponent],
      providers: [provideRouter([]), { provide: AuthService, useValue: authSvc }],
    }).compileComponents();
    const fixture = TestBed.createComponent(ImpersonationBannerComponent);
    router = TestBed.inject(Router);
    spyOn(router, 'navigate');
    fixture.detectChanges();
    return fixture;
  };

  it('banner assente durante sessione normale', async () => {
    authSvc = jasmine.createSpyObj('AuthService', ['isImpersonating', 'getImpersonatedEmail', 'endImpersonation']);
    authSvc.isImpersonating.and.returnValue(false);
    authSvc.getImpersonatedEmail.and.returnValue(null);

    const fixture = await setup();

    expect(fixture.nativeElement.querySelector('.impersonation-banner')).toBeNull();
  });

  it('banner visibile con email del target durante impersonazione', async () => {
    authSvc = jasmine.createSpyObj('AuthService', ['isImpersonating', 'getImpersonatedEmail', 'endImpersonation']);
    authSvc.isImpersonating.and.returnValue(true);
    authSvc.getImpersonatedEmail.and.returnValue('target@b.com');

    const fixture = await setup();

    const el = fixture.nativeElement.querySelector('.impersonation-banner');
    expect(el).not.toBeNull();
    expect(el.textContent).toContain('target@b.com');
  });

  it('click su "Esci da impersonazione" chiama endImpersonation e naviga a /dashboard', async () => {
    authSvc = jasmine.createSpyObj('AuthService', ['isImpersonating', 'getImpersonatedEmail', 'endImpersonation']);
    authSvc.isImpersonating.and.returnValue(true);
    authSvc.getImpersonatedEmail.and.returnValue('target@b.com');
    authSvc.endImpersonation.and.returnValue(of(undefined));

    const fixture = await setup();
    const button: HTMLButtonElement = fixture.nativeElement.querySelector('.impersonation-banner__exit');
    button.click();

    expect(authSvc.endImpersonation).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/dashboard']);
  });
});
