import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PwaInstallBannerComponent } from './pwa-install-banner.component';
import { PwaInstallService } from './pwa-install.service';
import { AuthService } from '../../auth/auth.service';

describe('PwaInstallBannerComponent', () => {
  let fixture: ComponentFixture<PwaInstallBannerComponent>;
  let installServiceMock: jasmine.SpyObj<PwaInstallService>;

  beforeEach(async () => {
    installServiceMock = jasmine.createSpyObj<PwaInstallService>('PwaInstallService', ['shouldShowBanner', 'dismiss', 'hasNativePrompt', 'triggerNativePrompt']);
    installServiceMock.shouldShowBanner.and.returnValue(true);

    await TestBed.configureTestingModule({
      imports: [PwaInstallBannerComponent],
      providers: [
        { provide: PwaInstallService, useValue: installServiceMock },
        { provide: AuthService, useValue: { isAuthenticated: signal(true) } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PwaInstallBannerComponent);
    fixture.detectChanges();
  });

  it('mostra il banner quando shouldShowBanner è true', () => {
    expect(fixture.nativeElement.querySelector('.install-banner')).toBeTruthy();
  });

  it('chiama dismiss() e nasconde il banner al click su "Non ora"', () => {
    const btn: HTMLButtonElement = fixture.nativeElement.querySelector('.btn-ghost');
    btn.click();
    fixture.detectChanges();
    expect(installServiceMock.dismiss).toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('.install-banner')).toBeFalsy();
  });

  it('aggiunge la classe has-pwa-install-banner al body quando visibile, la rimuove al dismiss', () => {
    expect(document.body.classList.contains('has-pwa-install-banner')).toBe(true);
    const btn: HTMLButtonElement = fixture.nativeElement.querySelector('.btn-ghost');
    btn.click();
    fixture.detectChanges();
    expect(document.body.classList.contains('has-pwa-install-banner')).toBe(false);
  });

  it('apre la guida al click su "Scopri come"', () => {
    const btn: HTMLButtonElement = fixture.nativeElement.querySelector('.btn-primary');
    btn.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('app-pwa-install-guide')).toBeTruthy();
  });
});

describe('PwaInstallBannerComponent (banner non mostrato)', () => {
  it('non mostra nulla se shouldShowBanner è false', async () => {
    const installServiceMock = jasmine.createSpyObj<PwaInstallService>('PwaInstallService', ['shouldShowBanner', 'dismiss', 'hasNativePrompt', 'triggerNativePrompt']);
    installServiceMock.shouldShowBanner.and.returnValue(false);

    await TestBed.configureTestingModule({
      imports: [PwaInstallBannerComponent],
      providers: [
        { provide: PwaInstallService, useValue: installServiceMock },
        { provide: AuthService, useValue: { isAuthenticated: signal(true) } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(PwaInstallBannerComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.install-banner')).toBeFalsy();
  });
});

describe('PwaInstallBannerComponent (utente non autenticato)', () => {
  it('non mostra il banner se l\'utente non ha fatto login, anche con shouldShowBanner true', async () => {
    const installServiceMock = jasmine.createSpyObj<PwaInstallService>('PwaInstallService', ['shouldShowBanner', 'dismiss', 'hasNativePrompt', 'triggerNativePrompt']);
    installServiceMock.shouldShowBanner.and.returnValue(true);

    await TestBed.configureTestingModule({
      imports: [PwaInstallBannerComponent],
      providers: [
        { provide: PwaInstallService, useValue: installServiceMock },
        { provide: AuthService, useValue: { isAuthenticated: signal(false) } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(PwaInstallBannerComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.install-banner')).toBeFalsy();
  });
});
