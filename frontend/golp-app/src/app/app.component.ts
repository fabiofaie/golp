import { Component, inject, OnInit } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { Router, RouterOutlet, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs';
import { AppUpdateService } from './shared/update/app-update.service';
import { AppUpdateBannerComponent } from './shared/update/app-update-banner.component';
import { PwaInstallBannerComponent } from './shared/pwa-install/pwa-install-banner.component';
import { ThemeService } from './theme/theme.service';
import { environment } from '../environments/environment';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, AppUpdateBannerComponent, PwaInstallBannerComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit {
  title = 'golp-app';

  private readonly doc = inject(DOCUMENT);
  private readonly router = inject(Router);
  private readonly updateService = inject(AppUpdateService);
  // Inject al bootstrap: registra l'effect del tema e applica la classe salvata (US-028)
  private readonly themeService = inject(ThemeService);

  ngOnInit(): void {
    this.doc.documentElement.style.setProperty('--color-brand', environment.brandColor);

    this.doc.addEventListener('visibilitychange', () => {
      if (this.doc.visibilityState === 'visible') {
        this.updateService.triggerCheck();
      }
    });

    this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe(() => this.updateService.triggerCheck());
  }
}
