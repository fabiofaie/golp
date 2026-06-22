import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PwaInstallGuideComponent } from './pwa-install-guide.component';
import { PwaPlatformService } from './pwa-platform.service';
import { PwaInstallService } from './pwa-install.service';

describe('PwaInstallGuideComponent', () => {
  let fixture: ComponentFixture<PwaInstallGuideComponent>;
  let platformMock: jasmine.SpyObj<PwaPlatformService>;
  let installServiceMock: jasmine.SpyObj<PwaInstallService>;

  function setup(os: 'ios' | 'android' | 'other', browser: 'safari' | 'chrome' | 'samsung' | 'firefox' | 'other', hasNativePrompt = false): void {
    platformMock = jasmine.createSpyObj<PwaPlatformService>('PwaPlatformService', ['detectOs', 'detectBrowser', 'isMobile', 'isStandalone']);
    platformMock.detectOs.and.returnValue(os);
    platformMock.detectBrowser.and.returnValue(browser);

    installServiceMock = jasmine.createSpyObj<PwaInstallService>('PwaInstallService', ['hasNativePrompt', 'triggerNativePrompt']);
    installServiceMock.hasNativePrompt.and.returnValue(hasNativePrompt);

    TestBed.configureTestingModule({
      imports: [PwaInstallGuideComponent],
      providers: [
        { provide: PwaPlatformService, useValue: platformMock },
        { provide: PwaInstallService, useValue: installServiceMock }
      ]
    });
    fixture = TestBed.createComponent(PwaInstallGuideComponent);
    fixture.detectChanges();
  }

  it('mostra gli step per iOS Safari', () => {
    setup('ios', 'safari');
    const steps = fixture.nativeElement.querySelectorAll('.step');
    expect(steps.length).toBeGreaterThan(0);
    expect(fixture.nativeElement.querySelector('.platform-badge').textContent).toContain('iOS · Safari');
  });

  it('mostra gli step per Android Chrome con azione installazione diretta disponibile', () => {
    setup('android', 'chrome', true);
    expect(fixture.nativeElement.querySelector('.install-guide__native')).toBeTruthy();
  });

  it('non mostra l\'azione diretta se hasNativePrompt è false anche su Chrome', () => {
    setup('android', 'chrome', false);
    expect(fixture.nativeElement.querySelector('.install-guide__native')).toBeFalsy();
  });

  it('mostra il fallback per combinazione non riconosciuta', () => {
    setup('other', 'other');
    const steps = fixture.nativeElement.querySelectorAll('.step');
    expect(steps.length).toBeGreaterThan(0);
  });

  it('emette close al click su "Torna indietro"', () => {
    setup('ios', 'safari');
    const emitSpy = jasmine.createSpy('close');
    fixture.componentInstance.close.subscribe(emitSpy);
    const btn: HTMLButtonElement = fixture.nativeElement.querySelector('.install-guide__back');
    btn.click();
    expect(emitSpy).toHaveBeenCalled();
  });

  it('triggerNativePrompt chiamato al click su "Installa ora"', () => {
    setup('android', 'chrome', true);
    fixture.detectChanges();
    const btn: HTMLButtonElement = fixture.nativeElement.querySelector('.btn-native');
    btn.click();
    expect(installServiceMock.triggerNativePrompt).toHaveBeenCalled();
  });
});
