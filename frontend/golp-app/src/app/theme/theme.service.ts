import { Injectable, signal, effect } from '@angular/core';

export type Theme = 'dark' | 'light';

const THEME_KEY = 'golp_theme';
const LIGHT_CLASS = 'theme-light';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly theme = signal<Theme>(this.readStored());

  constructor() {
    // Applica/rimuove la classe sul documento ad ogni cambio del signal.
    // Registrato nel costruttore: si attiva alla prima injection (bootstrap via AppComponent).
    effect(() => this.apply(this.theme()));
  }

  setTheme(theme: Theme): void {
    localStorage.setItem(THEME_KEY, theme);
    this.theme.set(theme);
  }

  private apply(theme: Theme): void {
    document.documentElement.classList.toggle(LIGHT_CLASS, theme === 'light');
  }

  private readStored(): Theme {
    return localStorage.getItem(THEME_KEY) === 'light' ? 'light' : 'dark';
  }
}
