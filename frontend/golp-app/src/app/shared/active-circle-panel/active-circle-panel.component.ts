import { Component, EventEmitter, Input, Output, inject, signal, computed } from '@angular/core';
import { ActiveCircleService } from '../active-circle.service';
import { CircleSummary } from '../../circles/circle.service';

export const ALL_CIRCLES_ID = 'all';
const SEARCH_THRESHOLD = 5;

/** Pannello bottom-sheet per la selezione del circolo attivo (US-066). */
@Component({
  selector: 'app-active-circle-panel',
  standalone: true,
  templateUrl: './active-circle-panel.component.html',
  styleUrl: './active-circle-panel.component.scss',
})
export class ActiveCirclePanelComponent {
  private readonly activeCircleService = inject(ActiveCircleService);

  @Input() open = false;
  @Output() closed = new EventEmitter<void>();

  readonly ALL_CIRCLES_ID = ALL_CIRCLES_ID;

  readonly searchQuery = signal('');

  readonly circles = this.activeCircleService.circles;
  readonly activeSelection = this.activeCircleService.activeSelection;
  readonly favoriteCircleIds = this.activeCircleService.favoriteCircleIds;

  readonly showSearch = computed(() => this.circles().length > SEARCH_THRESHOLD);

  private readonly filteredCircles = computed(() => {
    const query = this.searchQuery().trim().toLowerCase();
    const all = this.circles();
    if (!query) return all;
    return all.filter(c => c.name.toLowerCase().includes(query));
  });

  readonly favoriteCircles = computed(() =>
    this.filteredCircles().filter(c => this.favoriteCircleIds().has(c.id))
  );

  readonly otherCircles = computed(() =>
    this.filteredCircles().filter(c => !this.favoriteCircleIds().has(c.id))
  );

  readonly hasFavorites = computed(() => this.favoriteCircleIds().size > 0);

  isSelected(circleId: string): boolean {
    if (this.activeSelection() === circleId) return true;
    // Nessuna selezione esplicita: allinea l'evidenziazione al fallback pickActiveCircle
    // già usato da ActiveCircleService per determinare activeCircle() in dashboard.
    return this.activeSelection() === null && this.activeCircleService.activeCircle()?.id === circleId;
  }

  isFavorite(circleId: string): boolean {
    return this.favoriteCircleIds().has(circleId);
  }

  select(idOrAll: string): void {
    this.activeCircleService.selectCircle(idOrAll);
    this.close();
  }

  toggleFavorite(event: Event, circle: CircleSummary): void {
    event.stopPropagation();
    this.activeCircleService.toggleFavorite(circle.id);
  }

  close(): void {
    this.searchQuery.set('');
    this.closed.emit();
  }

  trackById(_index: number, circle: CircleSummary): string {
    return circle.id;
  }
}
