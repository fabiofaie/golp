import { CommonModule } from "@angular/common";
import { HttpClient } from "@angular/common/http";
import { Component, OnDestroy, OnInit, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { Router, RouterLink } from "@angular/router";
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from "rxjs";

import { environment } from "../../../environments/environment";
import { AuthService } from "../../auth/auth.service";
import { CircleService, SportConfig } from "../circle.service";
import { CirclePick, MatchService, PlayerSlotDto, QuickMatchResult, QuickCheckResponse, SuggestionUser } from "../match.service";
import { ShareConfirmComponent } from "../share-confirm/share-confirm.component";

interface QuickSlot {
  filled: boolean;
  userId?: string;
  displayName: string;
  isMe: boolean;
  isGuest: boolean;
  guestName?: string;
  guestEmail?: string;
  guestPhone?: string;
}

interface SetRow {
  team1: number | null;
  team2: number | null;
}

type Step = "sport" | "players" | "picker" | "score";

@Component({
  selector: "app-quick-match",
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ShareConfirmComponent],
  template: `
    <div class="qm-page">
      <header class="qm-header">
        <a routerLink="/dashboard" class="qm-back">← Indietro</a>
        <span class="qm-title">Registra Partita</span>
      </header>

      @if (quickMatchResult) {
        <main class="qm-main" data-testid="success-state">
          <h2 class="qm-section-title" style="margin-bottom:8px;">Partita Registrata!</h2>
          <p style="font-size:14px; color:var(--color-text-secondary); margin-bottom:24px;">
            Invia il link di conferma ai tuoi compagni di gioco.
          </p>
          @if (quickMatchResult.confirmationLinks.length > 0) {
            <app-share-confirm
              [links]="quickMatchResult.confirmationLinks"
              [sport]="selectedSport?.displayName ?? ''"
              [circleName]="quickMatchResult.circleName">
            </app-share-confirm>
          }
          <a routerLink="/dashboard" class="btn-primary" style="display:block; text-align:center; margin-top:24px; text-decoration:none;">
            Vai alla dashboard
          </a>
        </main>
      } @else {

      <!-- Stepper -->
      @if (step !== "picker") {
        <div class="qm-stepper">
          <span class="qm-step" [class.active]="step === 'sport'">1</span>
          <span class="qm-step-line"></span>
          <span class="qm-step" [class.active]="step === 'players'">2</span>
          <span class="qm-step-line"></span>
          <span class="qm-step" [class.active]="step === 'score'">3</span>
        </div>
      }

      <!-- Step 1: Sport -->
      @if (step === "sport") {
        <main class="qm-main">
          <h2 class="qm-section-title">Scegli lo sport</h2>
          <div class="qm-sport-grid">
            @for (s of sports; track s.sport) {
              <button class="qm-sport-card" (click)="selectSport(s)">
                <span class="qm-sport-name">{{ s.displayName }}</span>
              </button>
            }
          </div>
        </main>
      }

      <!-- Step 2: Players -->
      @if (step === "players") {
        <main class="qm-main">
          <h2 class="qm-section-title">Squadre</h2>

          @if (selectedSport?.allowsSingles) {
            <div style="display:flex; gap:8px; margin-bottom:4px;">
              <button type="button" class="slot-toggle-btn" [class.slot-toggle-btn--active]="!isSingles" (click)="toggleFormat(false)">Doppio</button>
              <button type="button" class="slot-toggle-btn" [class.slot-toggle-btn--active]="isSingles" (click)="toggleFormat(true)">Singolo</button>
            </div>
          }

          <div class="qm-teams">
            <div class="qm-team">
              <div class="qm-team-label team-a">Squadra A</div>
              @for (i of (isSingles ? [0] : [0, 1]); track i) {
                <div class="qm-slot" [class.filled]="slots[i].filled" [class.me-slot]="slots[i].isMe">
                  @if (slots[i].filled) {
                    <span class="qm-slot-name">{{ slots[i].displayName }}</span>
                    @if (!slots[i].isMe) {
                      <button class="qm-slot-remove" (click)="clearSlot(i)">×</button>
                    }
                    @if (slots[i].isMe) {
                      <span class="qm-slot-badge">Tu</span>
                    }
                    @if (slots[i].isGuest) {
                      <span class="qm-slot-guest-badge">ospite</span>
                    }
                  } @else {
                    <span class="qm-slot-placeholder">+ Aggiungi</span>
                  }
                </div>
              }
            </div>

            <div class="qm-team">
              <div class="qm-team-label team-b">Squadra B</div>
              @for (i of (isSingles ? [2] : [2, 3]); track i) {
                <div class="qm-slot" [class.filled]="slots[i].filled">
                  @if (slots[i].filled) {
                    <span class="qm-slot-name">{{ slots[i].displayName }}</span>
                    <button class="qm-slot-remove" (click)="clearSlot(i)">×</button>
                    @if (slots[i].isGuest) {
                      <span class="qm-slot-guest-badge">ospite</span>
                    }
                  } @else {
                    <span class="qm-slot-placeholder">+ Aggiungi</span>
                  }
                </div>
              }
            </div>
          </div>

          <!-- Search + chip cloud -->
          @if (!activeFilled) {
            <div class="qm-search-section">
              <input
                class="qm-search-input"
                type="text"
                placeholder="Cerca giocatore..."
                [(ngModel)]="searchQuery"
                (ngModelChange)="onSearch($event)" />

              @if (suggestions.length > 0) {
                <div class="qm-chip-cloud">
                  @for (s of suggestions; track s.userId) {
                    <button
                      class="qm-chip"
                      [class.used]="isUsed(s.userId)"
                      [disabled]="isUsed(s.userId)"
                      (click)="addFromSuggestion(s)">
                      {{ s.name }}
                    </button>
                  }
                </div>
              } @else if (searchQuery.length > 1 && !checkingCircles) {
                <p class="qm-no-results">Nessun giocatore trovato</p>
              }

              <!-- Guest form -->
              @if (!showGuestForm) {
                <button class="qm-add-guest-btn" (click)="showGuestForm = true">+ Aggiungi come ospite</button>
              } @else {
                <div class="qm-guest-form">
                  @if (contactPickerAvailable) {
                    <button type="button" class="qm-contact-picker-btn" (click)="pickContact()">
                      📇 Scegli dai contatti
                    </button>
                    <div class="qm-guest-divider">o inserisci manualmente</div>
                  }
                  <input class="qm-input" type="text" placeholder="Nome ospite *" [(ngModel)]="guestName" />
                  <input class="qm-input" type="email" placeholder="Email" [(ngModel)]="guestEmail" />
                  <input class="qm-input" type="tel" placeholder="Telefono" [(ngModel)]="guestPhone" />
                  @if (guestName.trim() && !guestEmail.trim() && !guestPhone.trim()) {
                    <p class="qm-guest-hint">Email o telefono obbligatorio</p>
                  }
                  <div class="qm-guest-form-actions">
                    <button class="btn-primary"
                      [disabled]="!guestName.trim() || (!guestEmail.trim() && !guestPhone.trim())"
                      (click)="addGuest()">Aggiungi</button>
                    <button class="btn-ghost" (click)="cancelGuest()">Annulla</button>
                  </div>
                </div>
              }
            </div>
          }

          @if (checkingCircles) {
            <p class="qm-checking">Cerco circoli...</p>
          }

          @if (errorMessage && !checkingCircles && !checkResult) {
            <div class="qm-error">{{ errorMessage }}</div>
          }

          <button
            class="btn-primary qm-cta"
            [disabled]="!activeFilled || checkingCircles"
            (click)="proceedFromPlayers()">
            Avanti →
          </button>
        </main>
      }

      <!-- Circle picker -->
      @if (step === "picker") {
        <main class="qm-main">
          <h2 class="qm-section-title">
            {{ checkResult?.mode === "exact" ? "Con quale circolo?" : "Dove registrare?" }}
          </h2>
          <p class="qm-picker-hint">
            {{
              checkResult?.mode === "partial"
                ? "Trovati circoli con i giocatori noti. Scegli o crea un nuovo gruppo."
                : "Questi 4 giocatori si trovano in più circoli."
            }}
          </p>

          <div class="qm-circle-list">
            @for (c of checkResult!.circles; track c.id) {
              <button class="qm-circle-item" (click)="pickCircle(c)">
                <span class="qm-circle-name">{{ c.name }}</span>
                @if (c.lastMatchAt) {
                  <span class="qm-circle-date">Ultima partita: {{ c.lastMatchAt | date: "dd/MM/yy" }}</span>
                }
              </button>
            }
            @if (checkResult?.mode === "partial") {
              <button class="qm-circle-item qm-circle-new" (click)="pickNewCircle()">+ Crea nuovo gruppo</button>
            }
          </div>
        </main>
      }

      <!-- Step 3: Score (+4 name if new circle) -->
      @if (step === "score") {
        <main class="qm-main">
          <!-- Circle banner -->
          @if (selectedCircle) {
            <div class="qm-info-banner">
              📍 Stai registrando in <strong>{{ selectedCircle.name }}</strong>
            </div>
          }

          <!-- Team recap -->
          <div class="qm-score-teams">
            <div class="qm-score-team">
              <div
                class="qm-team-label team-a"
                style="font-size:10px;letter-spacing:.06em;text-transform:uppercase;margin-bottom:4px">
                Squadra A
              </div>
              <div style="font-size:12px;color:var(--color-text-secondary);line-height:1.4">
                {{ slots[0].displayName }}@if (!isSingles) {<br />{{ slots[1].displayName }}}
              </div>
            </div>
            <div class="qm-score-vs">VS</div>
            <div class="qm-score-team">
              <div
                class="qm-team-label team-b"
                style="font-size:10px;letter-spacing:.06em;text-transform:uppercase;margin-bottom:4px">
                Squadra B
              </div>
              <div style="font-size:12px;color:var(--color-text-secondary);line-height:1.4">
                {{ slots[2].displayName }}@if (!isSingles) {<br />{{ slots[3].displayName }}}
              </div>
            </div>
          </div>

          <!-- Sets -->
          <div>
            <p class="section-label">Punteggio{{ selectedSport?.sets ? " (set)" : "" }}</p>

            @if (selectedSport?.sets) {
              <!-- header Sq.1 / Sq.2 -->
              <div class="score-row" style="margin-bottom:4px;">
                <span style="width:36px; flex-shrink:0;"></span>
                <div class="score-inputs">
                  <div class="score-input-wrap" style="text-align:center;">
                    <span class="score-team-label score-team-label--t1">SQ.1</span>
                  </div>
                  <span style="visibility:hidden;" class="score-dash">—</span>
                  <div class="score-input-wrap" style="text-align:center;">
                    <span class="score-team-label score-team-label--t2">SQ.2</span>
                  </div>
                </div>
                <span style="width:24px; flex-shrink:0;"></span>
              </div>

              @for (set of sets; track $index; let i = $index) {
                <div class="score-row">
                  <span class="score-set-label">Set {{ i + 1 }}</span>
                  <div class="score-inputs">
                    <div class="score-input-wrap">
                      <input type="number" class="score-input score-input--team1"
                             placeholder="0" min="0" [(ngModel)]="set.team1" />
                    </div>
                    <span class="score-dash">—</span>
                    <div class="score-input-wrap">
                      <input type="number" class="score-input score-input--team2"
                             placeholder="0" min="0" [(ngModel)]="set.team2" />
                    </div>
                  </div>
                  <button type="button" class="score-remove-btn"
                          (click)="removeSet(i)" [disabled]="sets.length <= 1">×</button>
                </div>
              }

              <button type="button" class="add-set-btn" (click)="addSet()">
                <svg width="14" height="14" viewBox="0 0 14 14" fill="none">
                  <path d="M7 2v10M2 7h10" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/>
                </svg>
                Aggiungi set
              </button>

            } @else {
              <!-- single score mode -->
              <div style="display:flex; align-items:center; gap:16px; margin-bottom:24px;">
                <div style="flex:1; text-align:center;">
                  <div class="score-team-label score-team-label--t1" style="margin-bottom:8px;">Squadra A</div>
                  <input type="number" class="score-single-input score-single-input--t1"
                         placeholder="0" min="0" [(ngModel)]="sets[0].team1" />
                </div>
                <span class="score-dash" style="font-size:var(--font-size-xl); margin-top:20px;">–</span>
                <div style="flex:1; text-align:center;">
                  <div class="score-team-label score-team-label--t2" style="margin-bottom:8px;">Squadra B</div>
                  <input type="number" class="score-single-input score-single-input--t2"
                         placeholder="0" min="0" [(ngModel)]="sets[0].team2" />
                </div>
              </div>
            }
          </div>

          <!-- Step 4: circle name (new group only) -->
          @if (!selectedCircle) {
            <div class="qm-name-section">
              <h2 class="qm-section-title">Nome del gruppo</h2>
              <input
                class="qm-input"
                type="text"
                [(ngModel)]="newCircleName"
                placeholder="Es. Padel con Marco e Luca" />
            </div>
          }

          @if (errorMessage) {
            <div class="qm-error">{{ errorMessage }}</div>
          }

          <button class="btn-primary qm-cta" [disabled]="!canSubmit() || isSubmitting" (click)="submit()">
            {{ isSubmitting ? "Registrazione..." : "Registra Partita" }}
          </button>
        </main>
      }

      } <!-- end @else (quickMatchResult) -->
    </div>
  `,
  styles: [
    `
      .qm-page {
        min-height: 100vh;
        background: var(--color-bg);
        color: var(--color-text);
        font-family: inherit;
      }

      .qm-header {
        display: flex;
        align-items: center;
        gap: 16px;
        padding: 16px;
        border-bottom: 1px solid var(--color-border);
        background: var(--color-surface);
      }

      .qm-back {
        color: var(--color-text-secondary);
        text-decoration: none;
        font-size: 14px;
      }

      .qm-title {
        font-weight: 700;
        font-size: 16px;
      }

      .qm-main {
        padding: 20px 16px;
        max-width: 480px;
        margin: 0 auto;
        display: flex;
        flex-direction: column;
        gap: 16px;
      }

      .qm-stepper {
        display: flex;
        align-items: center;
        justify-content: center;
        gap: 8px;
        padding: 16px;
      }

      .qm-step {
        width: 28px;
        height: 28px;
        border-radius: 50%;
        background: var(--color-surface);
        border: 1px solid var(--color-border);
        display: flex;
        align-items: center;
        justify-content: center;
        font-size: 12px;
        font-weight: 700;
        color: var(--color-text-secondary);
      }

      .qm-step.active {
        background: var(--color-accent);
        border-color: var(--color-accent);
        color: #fff;
      }

      .qm-step-line {
        flex: 1;
        height: 1px;
        background: var(--color-border);
        max-width: 40px;
      }

      .qm-section-title {
        font-size: 16px;
        font-weight: 700;
        margin: 0;
      }

      /* Sport grid */
      .qm-sport-grid {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 12px;
      }

      .qm-sport-card {
        background: var(--color-surface);
        border: 1px solid var(--color-border);
        border-radius: 8px;
        padding: 24px 16px;
        font-size: 15px;
        font-weight: 600;
        color: var(--color-text);
        cursor: pointer;
        text-align: center;
        transition:
          border-color 0.15s,
          background 0.15s;
      }

      .qm-sport-card:hover {
        border-color: var(--color-accent);
        background: var(--color-surface-hover, var(--color-surface));
      }

      .qm-sport-name {
        display: block;
      }

      /* Teams + slots */
      .qm-teams {
        display: flex;
        flex-direction: column;
        gap: 12px;
      }

      .qm-team {
        display: flex;
        flex-direction: column;
        gap: 6px;
      }

      .qm-team-label {
        font-size: 11px;
        font-weight: 700;
        letter-spacing: 0.06em;
        text-transform: uppercase;
      }

      .team-a {
        color: var(--color-accent);
      }
      .team-b {
        color: var(--color-info, #4a9eff);
      }

      .qm-slot {
        display: flex;
        align-items: center;
        gap: 8px;
        padding: 10px 12px;
        border-radius: 6px;
        border: 1px dashed var(--color-border);
        background: var(--color-surface);
        min-height: 42px;
      }

      .qm-slot.filled {
        border-style: solid;
      }

      .qm-slot.me-slot {
        border-color: var(--color-accent);
        background: color-mix(in srgb, var(--color-accent) 12%, var(--color-surface));
      }

      .qm-slot-name {
        flex: 1;
        font-size: 14px;
        font-weight: 500;
      }

      .qm-slot-placeholder {
        flex: 1;
        color: var(--color-text-secondary);
        font-size: 13px;
      }

      .qm-slot-remove {
        background: none;
        border: none;
        color: var(--color-text-secondary);
        cursor: pointer;
        font-size: 16px;
        padding: 0 4px;
      }

      .qm-slot-badge {
        font-size: 10px;
        font-weight: 700;
        background: var(--color-accent);
        color: #fff;
        border-radius: 4px;
        padding: 2px 6px;
      }

      .qm-slot-guest-badge {
        font-size: 10px;
        font-weight: 700;
        background: var(--color-surface);
        color: var(--color-text-secondary);
        border: 1px solid var(--color-border);
        border-radius: 4px;
        padding: 2px 6px;
      }

      /* Search + chips */
      .qm-search-section {
        display: flex;
        flex-direction: column;
        gap: 10px;
      }

      .qm-search-input {
        width: 100%;
        padding: 10px 12px;
        border-radius: 6px;
        border: 1px solid var(--color-border);
        background: var(--color-surface);
        color: var(--color-text);
        font-size: 14px;
        box-sizing: border-box;
      }

      .qm-chip-cloud {
        display: flex;
        flex-wrap: wrap;
        gap: 8px;
      }

      .qm-chip {
        padding: 6px 12px;
        border-radius: 20px;
        border: 1px solid var(--color-border);
        background: var(--color-surface);
        color: var(--color-text);
        font-size: 13px;
        cursor: pointer;
        transition: border-color 0.15s;
      }

      .qm-chip:hover:not(.used) {
        border-color: var(--color-accent);
      }

      .qm-chip.used {
        opacity: 0.35;
        cursor: default;
      }

      .qm-no-results {
        color: var(--color-text-secondary);
        font-size: 13px;
        margin: 0;
      }

      .qm-add-guest-btn {
        align-self: flex-start;
        background: none;
        border: none;
        color: var(--color-accent);
        font-size: 13px;
        cursor: pointer;
        padding: 0;
      }

      .qm-guest-form {
        display: flex;
        flex-direction: column;
        gap: 8px;
        padding: 12px;
        border: 1px solid var(--color-border);
        border-radius: 8px;
        background: var(--color-surface);
      }

      .qm-contact-picker-btn {
        width: 100%;
        padding: 10px;
        border-radius: 8px;
        border: 1px solid var(--color-accent);
        background: none;
        color: var(--color-accent);
        font-size: 14px;
        font-weight: 600;
        cursor: pointer;
      }

      .qm-guest-divider {
        text-align: center;
        font-size: 11px;
        color: var(--color-text-secondary);
      }

      .qm-guest-hint {
        font-size: 11px;
        color: var(--color-text-secondary);
        margin: 0;
      }

      .qm-guest-form-actions {
        display: flex;
        gap: 8px;
      }

      .qm-input {
        width: 100%;
        padding: 10px 12px;
        border-radius: 6px;
        border: 1px solid var(--color-border);
        background: var(--color-surface);
        color: var(--color-text);
        font-size: 14px;
        box-sizing: border-box;
      }

      .qm-checking {
        color: var(--color-text-secondary);
        font-size: 13px;
        margin: 0;
        text-align: center;
      }

      .qm-cta {
        width: 100%;
        margin-top: 8px;
      }

      /* Picker */
      .qm-picker-hint {
        color: var(--color-text-secondary);
        font-size: 13px;
        margin: 0;
      }

      .qm-circle-list {
        display: flex;
        flex-direction: column;
        gap: 8px;
      }

      .qm-circle-item {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 14px 16px;
        border-radius: 8px;
        border: 1px solid var(--color-border);
        background: var(--color-surface);
        color: var(--color-text);
        cursor: pointer;
        text-align: left;
        width: 100%;
        transition: border-color 0.15s;
      }

      .qm-circle-item:hover {
        border-color: var(--color-accent);
      }

      .qm-circle-new {
        border-style: dashed;
        color: var(--color-accent);
        justify-content: center;
      }

      .qm-circle-name {
        font-size: 15px;
        font-weight: 500;
        color: var(--color-text);
      }

      .qm-circle-date {
        font-size: 12px;
        color: var(--color-text-secondary);
      }

      /* Score step */
      .qm-info-banner {
        padding: 12px 16px;
        border-radius: 8px;
        background: color-mix(in srgb, var(--color-accent) 15%, var(--color-surface));
        border: 1px solid var(--color-accent);
        font-size: 14px;
      }

      .qm-score-teams {
        display: flex;
        align-items: center;
        gap: 12px;
        padding: 16px;
        background: var(--color-surface);
        border: 1px solid var(--color-border);
        border-radius: 12px;
        margin-bottom: 24px;
      }

      .qm-score-team {
        flex: 1;
      }

      .qm-score-vs {
        font-size: 11px;
        font-weight: 700;
        color: var(--color-text-secondary);
        flex-shrink: 0;
      }

      .qm-name-section {
        display: flex;
        flex-direction: column;
        gap: 8px;
        border-top: 1px solid var(--color-border);
        padding-top: 16px;
      }

      .qm-error {
        padding: 12px;
        border-radius: 6px;
        background: color-mix(in srgb, #ff4444 15%, var(--color-surface));
        border: 1px solid #ff4444;
        font-size: 13px;
        color: var(--color-text);
      }

      .slot-toggle-btn {
        flex: 1;
        padding: 8px 0;
        border-radius: 6px;
        border: 1px solid var(--color-border);
        background: var(--color-surface);
        color: var(--color-text-secondary);
        font-size: 13px;
        font-weight: 600;
        cursor: pointer;
        transition: all 0.15s;
      }
      .slot-toggle-btn--active {
        border-color: var(--color-accent);
        background: color-mix(in srgb, var(--color-accent) 12%, var(--color-surface));
        color: var(--color-accent);
      }
    `
  ]
})
export class QuickMatchComponent implements OnInit, OnDestroy {
  private readonly router = inject(Router);
  private readonly authSvc = inject(AuthService);
  private readonly circleSvc = inject(CircleService);
  private readonly matchSvc = inject(MatchService);
  private readonly http = inject(HttpClient);
  private readonly destroy$ = new Subject<void>();
  private readonly search$ = new Subject<string>();

  step: Step = "sport";
  sports: SportConfig[] = [];

  selectedSport: SportConfig | null = null;
  isSingles = false;

  get activeSlotIndices(): number[] { return this.isSingles ? [0, 2] : [0, 1, 2, 3]; }
  get activeFilled(): boolean { return this.activeSlotIndices.every(i => this.slots[i].filled); }

  currentUserId = "";
  currentUserName = "";

  slots: QuickSlot[] = [
    { filled: false, displayName: "", isMe: true, isGuest: false },
    { filled: false, displayName: "", isMe: false, isGuest: false },
    { filled: false, displayName: "", isMe: false, isGuest: false },
    { filled: false, displayName: "", isMe: false, isGuest: false }
  ];

  suggestions: SuggestionUser[] = [];
  searchQuery = "";
  showGuestForm = false;
  guestName = "";
  guestEmail = "";
  guestPhone = "";

  readonly contactPickerAvailable: boolean =
    typeof navigator !== 'undefined' &&
    'contacts' in navigator &&
    'ContactsManager' in window;
  checkingCircles = false;

  checkResult: QuickCheckResponse | null = null;
  selectedCircle: CirclePick | null = null;

  sets: SetRow[] = [{ team1: null, team2: null }];
  newCircleName = "";

  isSubmitting = false;
  errorMessage = "";
  quickMatchResult: QuickMatchResult | null = null;

  get filledCount(): number {
    return this.activeSlotIndices.filter(i => this.slots[i].filled).length;
  }

  get totalRequired(): number { return this.isSingles ? 2 : 4; }

  ngOnInit(): void {
    this.currentUserId = this.authSvc.getCurrentUserId() ?? "";

    // Load user name from /auth/me
    this.http
      .get<{ id: string; name: string; email: string }>(`${environment.apiUrl}/auth/me`)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (me) => {
          this.currentUserName = me.name;
          this.slots[0] = { filled: true, userId: me.id, displayName: me.name, isMe: true, isGuest: false };
        }
      });

    // Load sports
    this.circleSvc
      .getSports()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (sports) => (this.sports = sports)
      });

    // Search debounce
    this.search$
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe((q) => this.loadSuggestions(q));
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  selectSport(sport: SportConfig): void {
    this.selectedSport = sport;
    this.isSingles = false;
    this.step = "players";
    this.loadSuggestions("");
  }

  toggleFormat(singles: boolean): void {
    this.isSingles = singles;
    // clear inactive slots when switching to singles
    if (singles) {
      this.slots[1] = { filled: false, displayName: '', isMe: false, isGuest: false };
      this.slots[3] = { filled: false, displayName: '', isMe: false, isGuest: false };
    }
    this.checkResult = null;
    this.selectedCircle = null;
    if (this.activeFilled) this.runCheck();
  }

  onSearch(q: string): void {
    this.search$.next(q);
  }

  private loadSuggestions(q: string): void {
    if (!this.selectedSport) return;
    this.matchSvc
      .getSuggestions(this.selectedSport.sport, q || undefined)
      .pipe(takeUntil(this.destroy$))
      .subscribe({ next: (s) => (this.suggestions = s) });
  }

  isUsed(userId: string): boolean {
    return this.slots.some((s) => s.filled && s.userId === userId);
  }

  addFromSuggestion(s: SuggestionUser): void {
    const idx = this.activeSlotIndices.find(i => !this.slots[i].filled);
    if (idx === undefined) return;
    this.slots[idx] = {
      filled: true,
      userId: s.userId,
      displayName: s.name,
      isMe: false,
      isGuest: false
    };
    this.onSlotsChanged();
  }

  clearSlot(i: number): void {
    if (this.slots[i].isMe) return;
    this.slots[i] = { filled: false, displayName: "", isMe: false, isGuest: false };
    this.checkResult = null;
    this.selectedCircle = null;
  }

  addGuest(): void {
    const idx = this.activeSlotIndices.find(i => !this.slots[i].filled);
    if (idx === undefined || !this.guestName.trim()) return;
    this.slots[idx] = {
      filled: true,
      displayName: this.guestName.trim(),
      isMe: false,
      isGuest: true,
      guestName: this.guestName.trim(),
      guestEmail: this.guestEmail.trim() || undefined,
      guestPhone: this.guestPhone.trim() || undefined,
    };
    this.guestName = '';
    this.guestEmail = '';
    this.guestPhone = '';
    this.showGuestForm = false;
    this.onSlotsChanged();
  }

  cancelGuest(): void {
    this.guestName = '';
    this.guestEmail = '';
    this.guestPhone = '';
    this.showGuestForm = false;
  }

  async pickContact(): Promise<void> {
    if (!this.contactPickerAvailable) return;
    try {
      const contacts: Array<{ name?: string[]; tel?: string[] }> =
        await (navigator as any).contacts.select(['name', 'tel'], { multiple: false });
      if (contacts.length === 0) return;
      const c = contacts[0];
      this.guestName = c.name?.[0] ?? this.guestName;
      this.guestPhone = c.tel?.[0] ?? this.guestPhone;
    } catch {
      // user cancelled or API not supported
    }
  }

  private onSlotsChanged(): void {
    if (this.activeFilled) {
      this.runCheck();
    }
  }

  private runCheck(): void {
    if (!this.selectedSport) return;
    this.checkingCircles = true;
    this.checkResult = null;
    this.errorMessage = "";

    const activeSlots = this.activeSlotIndices.map(i => this.slots[i]);
    const userIds = activeSlots.filter((s) => s.filled && !s.isGuest).map((s) => s.userId!);
    const guests = activeSlots
      .filter((s) => s.filled && s.isGuest)
      .map((s) => ({ email: s.guestEmail, phone: s.guestPhone }));

    this.matchSvc
      .checkQuickMatch({
        sport: this.selectedSport.sport,
        userIds,
        guests,
        isSingles: this.isSingles,
      })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result) => {
          this.checkResult = result;
          this.checkingCircles = false;
        },
        error: () => {
          this.checkingCircles = false;
          this.errorMessage = "Errore nella verifica dei circoli. Riprova.";
        }
      });
  }

  proceedFromPlayers(): void {
    if (!this.activeFilled || this.checkingCircles) return;
    if (!this.checkResult) return;

    const { mode, circles } = this.checkResult;

    if (mode === "exact") {
      if (circles.length === 1) {
        this.selectedCircle = circles[0];
        this.step = "score";
      } else if (circles.length > 1) {
        this.step = "picker";
      } else {
        this.selectedCircle = null;
        this.buildAutoName();
        this.step = "score";
      }
    } else {
      // partial
      if (circles.length > 0) {
        this.step = "picker";
      } else {
        this.selectedCircle = null;
        this.buildAutoName();
        this.step = "score";
      }
    }
  }

  pickCircle(c: CirclePick): void {
    this.selectedCircle = c;
    this.step = "score";
  }

  pickNewCircle(): void {
    this.selectedCircle = null;
    this.buildAutoName();
    this.step = "score";
  }

  private buildAutoName(): void {
    if (!this.selectedSport) return;
    const n1 = this.slots[1]?.displayName ?? "?";
    const n2 = this.slots[2]?.displayName ?? "?";
    this.newCircleName = `${this.selectedSport.displayName} con ${n1} e ${n2}`;
  }

  addSet(): void {
    this.sets.push({ team1: null, team2: null });
  }

  removeSet(i: number): void {
    if (this.sets.length <= 1) return;
    this.sets.splice(i, 1);
  }

  canSubmit(): boolean {
    if (!this.selectedSport) return false;
    if (this.sets.some((s) => s.team1 === null || s.team2 === null)) return false;
    if (!this.selectedCircle && !this.newCircleName.trim()) return false;
    return true;
  }

  submit(): void {
    if (!this.canSubmit() || this.isSubmitting || !this.selectedSport) return;
    this.isSubmitting = true;
    this.errorMessage = "";

    const toSlotDto = (slot: QuickSlot): PlayerSlotDto =>
      slot.isGuest
        ? { guestName: slot.guestName, guestEmail: slot.guestEmail, guestPhone: slot.guestPhone }
        : { userId: slot.userId };

    this.matchSvc
      .createQuickMatch({
        sport: this.selectedSport.sport,
        circleId: this.selectedCircle?.id,
        circleName: this.selectedCircle ? undefined : this.newCircleName.trim(),
        team1: this.isSingles ? [toSlotDto(this.slots[0])] : [toSlotDto(this.slots[0]), toSlotDto(this.slots[1])],
        team2: this.isSingles ? [toSlotDto(this.slots[2])] : [toSlotDto(this.slots[2]), toSlotDto(this.slots[3])],
        sets: this.sets.map((s) => ({ team1: s.team1!, team2: s.team2! })),
        isSingles: this.isSingles,
      })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result) => {
          this.isSubmitting = false;
          this.quickMatchResult = result;
        },
        error: (err) => {
          this.isSubmitting = false;
          this.errorMessage = err?.error?.error ?? "Errore durante la registrazione.";
        }
      });
  }
}
