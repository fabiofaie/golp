import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ProfileComponent } from './profile.component';
import { ThemeService } from '../theme/theme.service';

describe('ProfileComponent', () => {
  let fixture: ComponentFixture<ProfileComponent>;
  let theme: ThemeService;

  beforeEach(() => {
    localStorage.clear();
    document.documentElement.classList.remove('theme-light');

    TestBed.configureTestingModule({
      imports: [ProfileComponent],
      providers: [provideRouter([])]
    });
    fixture = TestBed.createComponent(ProfileComponent);
    theme = TestBed.inject(ThemeService);
    fixture.detectChanges();
  });

  afterEach(() => {
    localStorage.clear();
    document.documentElement.classList.remove('theme-light');
  });

  function options(): HTMLButtonElement[] {
    return Array.from(fixture.nativeElement.querySelectorAll('.theme-option'));
  }

  it('con localStorage vuoto mostra "Scuro" come attivo', () => {
    const [dark, light] = options();
    expect(dark.classList).toContain('theme-option--active');
    expect(light.classList).not.toContain('theme-option--active');
  });

  it('click su "Chiaro" imposta il tema light, persiste e applica la classe', () => {
    const [, light] = options();
    light.click();
    fixture.detectChanges();
    TestBed.flushEffects();

    expect(theme.theme()).toBe('light');
    expect(localStorage.getItem('golp_theme')).toBe('light');
    expect(document.documentElement.classList.contains('theme-light')).toBe(true);
    expect(light.classList).toContain('theme-option--active');
  });

  it('click su "Scuro" dopo "Chiaro" torna a dark e rimuove la classe', () => {
    const [dark, light] = options();
    light.click();
    fixture.detectChanges();
    dark.click();
    fixture.detectChanges();
    TestBed.flushEffects();

    expect(theme.theme()).toBe('dark');
    expect(document.documentElement.classList.contains('theme-light')).toBe(false);
  });
});
