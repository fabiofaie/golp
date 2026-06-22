import { Component, inject, OnInit } from '@angular/core';
import { Router, RouterOutlet, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs';
import { AppUpdateService } from './shared/update/app-update.service';
import { AppUpdateBannerComponent } from './shared/update/app-update-banner.component';
import { PwaInstallBannerComponent } from './shared/pwa-install/pwa-install-banner.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, AppUpdateBannerComponent, PwaInstallBannerComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit {
  title = 'golp-app';

  private readonly router = inject(Router);
  private readonly updateService = inject(AppUpdateService);

  ngOnInit(): void {
    document.addEventListener('visibilitychange', () => {
      if (document.visibilityState === 'visible') {
        this.updateService.triggerCheck();
      }
    });

    this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe(() => this.updateService.triggerCheck());
  }
}
