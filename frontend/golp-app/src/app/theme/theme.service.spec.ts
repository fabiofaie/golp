import { TestBed } from '@angular/core/testing';
import { ThemeService } from './theme.service';

describe('ThemeService', () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.classList.remove('theme-light');
  });

  afterEach(() => {
    localStorage.clear();
    document.documentElement.classList.remove('theme-light');
  });

  function create(): ThemeService {
    TestBed.configureTestingModule({});
    return TestBed.inject(ThemeService);
  }

  it('default scuro quando localStorage vuoto', () => {
    const svc = create();
    expect(svc.theme()).toBe('dark');
    expect(document.documentElement.classList.contains('theme-light')).toBe(false);
  });

  it('legge tema chiaro da localStorage e applica la classe', () => {
    localStorage.setItem('golp_theme', 'light');
    const svc = create();
    TestBed.flushEffects();
    expect(svc.theme()).toBe('light');
    expect(document.documentElement.classList.contains('theme-light')).toBe(true);
  });

  it('valore invalido in localStorage → default scuro', () => {
    localStorage.setItem('golp_theme', 'banana');
    const svc = create();
    expect(svc.theme()).toBe('dark');
  });

  it('setTheme persiste in localStorage e aggiorna il signal', () => {
    const svc = create();
    svc.setTheme('light');
    expect(localStorage.getItem('golp_theme')).toBe('light');
    expect(svc.theme()).toBe('light');
  });

  it('setTheme applica/rimuove la classe theme-light su documentElement', () => {
    const svc = create();
    svc.setTheme('light');
    TestBed.flushEffects();
    expect(document.documentElement.classList.contains('theme-light')).toBe(true);
    svc.setTheme('dark');
    TestBed.flushEffects();
    expect(document.documentElement.classList.contains('theme-light')).toBe(false);
  });
});
