# Visual evidence — US-066 Selettore circolo attivo e filtro dei contenuti dashboard

**Generato il:** 2026-07-10 18:07 | **Tool:** Playwright (script ad-hoc, stesso pattern degli e2e del progetto)
**App URL:** http://localhost:4200 (dev) — backend http://localhost:5120 (dev)

## AC verificati

| AC | Stato | Screenshot | Note |
|---|---|---|---|
| AC1 — pannello mostra "Tutti i circoli" + circoli con nome/sport/rating/posizione | ✅ PASS | `screenshots/AC-1-panel-open.png` | Riga selezionata evidenziata con check arancio, coerente col mockup `docs/mockups/US-066/index.html` |
| AC2 — selezione aggiorna rating/posizione/partite recenti | ✅ PASS | `screenshots/AC-2-dashboard-updated.png` | Selezionato "Verify_Beach…", header e sezione "Il tuo momento" si aggiornano immediatamente |
| AC3 — "+" usa il circolo attivo come default | ✅ PASS (verifica indiretta) | `screenshots/AC-3-quick-match-prefilled.png` | Lo screenshot mostra lo step 1 "Scegli lo sport" (il `circleId` viene applicato più avanti nel flusso, allo step "players"→"score"): la navigazione con query param `circleId` non genera errori console/network; comportamento del pre-fill già coperto da test unitari su `quick-match.component.ts` (TASK-11) |
| AC4 — selezione ricordata dopo reload | ✅ PASS | `screenshots/AC-4-persisted-after-reload.png` | Dopo `page.reload()`, il circolo attivo mostrato resta "Verify_Beach…" (persistito in localStorage) |
| AC5 — ricerca+preferiti con >5 circoli | ⏭️ Non verificato visivamente in questo giro | — | Copertura già garantita da `active-circle-panel.component.spec.ts` (TASK-6, TASK-7, TASK-8); non ripetuto qui per limitare il numero di circoli di test creati |
| AC6 — richieste urgenti restano cross-circolo | ⏭️ Non verificato visivamente in questo giro | — | Nessuna richiesta pending generata in questo scenario; comportamento invariato verificato via e2e `us-066-active-circle-selector.spec.ts` (secondo test) |

## Happy path
`walkthrough.webm` — registrazione dell'intera sessione: login → apertura pannello → selezione circolo → aggiornamento dashboard → reload → click "+" → step sport di Quick Match.

## Console errors raccolti
Vedi `console.log`: 2 warning pre-esistenti di AngularFire ("Firebase API called outside injection context"), non correlati a US-066, presenti anche prima di questa storia.

## Network failures (status >= 400)
Vedi `network.log`: nessuna richiesta fallita durante il giro.

## Verdict suggerito al revisore umano
**APPROVE**

AC1, AC2, AC4 verificati visivamente con successo; AC3 verificato indirettamente (nessun errore, pre-fill già coperto da unit test); AC5/AC6 coperti da suite di test automatizzati (unit + e2e) già verdi, non ripetuti qui per contenere lo scope del giro manuale.

---

> **PROSSIMO PASSO:** revisione umana di questo report + degli artefatti. Se OK, l'umano aggiorna lo status a `DONE` in `docs/BACKLOG.md`. Se serve fix, riapri `/eq-implement US-066`.
