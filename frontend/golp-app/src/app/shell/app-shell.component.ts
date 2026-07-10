import { Component, inject, signal } from '@angular/core';
import { Router, RouterOutlet, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs/operators';
import { BottomNavComponent } from '../shared/bottom-nav/bottom-nav.component';

const MAIN_ROUTES = ['/dashboard', '/my-matches', '/circles', '/profilo'];

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterOutlet, BottomNavComponent],
  templateUrl: './app-shell.component.html',
})
export class AppShellComponent {
  private readonly router = inject(Router);

  readonly showBottomNav = signal(this.isMainRoute(this.router.url));

  constructor() {
    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe(e => this.showBottomNav.set(this.isMainRoute(e.urlAfterRedirects)));
  }

  private isMainRoute(url: string): boolean {
    const path = url.split('?')[0].split('#')[0];
    return MAIN_ROUTES.includes(path);
  }
}
