import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { ActiveCircleService } from '../active-circle.service';

@Component({
  selector: 'app-bottom-nav',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './bottom-nav.component.html',
  styleUrl: './bottom-nav.component.scss',
})
export class BottomNavComponent {
  readonly activeCircle = inject(ActiveCircleService);

  constructor() {
    // La bottom-nav è montata globalmente (US-064): il circolo attivo deve
    // caricarsi anche se l'utente non passa mai da /dashboard in questa sessione
    // (bookmark, deep link, refresh su /my-matches, /circles, /profilo).
    this.activeCircle.ensureLoaded();
  }

  // Il CTA "+" è ora globale (visibile su tutte le route principali, US-064): la guardia
  // sui 4 membri resta intenzionalmente globale anch'essa, per dare feedback ovunque
  // il CTA sia cliccabile (decisione presa in review, non ristretta a /dashboard).
  onRecordMatchClick(): void {
    this.activeCircle.onRecordMatchClick();
  }
}
