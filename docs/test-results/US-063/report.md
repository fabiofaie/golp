# Visual evidence — US-063 Redesign dashboard con gerarchia azioni urgenti/contesto/personale/storico

**Generato il:** 2026-07-10 01:00 | **Tool:** Playwright (script ad-hoc + e2e `us-063-dashboard.spec.ts`)
**App URL:** http://localhost:4200 (dev) — backend http://localhost:5120

## AC verificati

| AC | Stato | Screenshot | Note |
|---|---|---|---|
| AC1 — azioni urgenti primo blocco, cross-circolo | ✅ PASS | `screenshots/AC1-AC2-AC3-AC4.png` | Sezione "Richiede attenzione" visibile in cima con badge "DA CONFERMARE" e circolo di appartenenza |
| AC2 — card circolo attivo (rating/posizione/stats) | ✅ PASS | `screenshots/AC1-AC2-AC3-AC4.png` | Circolo attivo = il più vecchio per iscrizione, come da criterio provvisorio |
| AC3 — serie vittorie correnti | ✅ PASS | `screenshots/AC1-AC2-AC3-AC4.png` | 1 vittoria confirmed → serie = 1 |
| AC4 — ultime partite con esito/avversari | ✅ PASS | `screenshots/AC1-AC2-AC3-AC4.png` | Lista "Ultime partite" popolata |
| AC5 — nessun CTA duplicato fuori dal "+" | ✅ PASS | `screenshots/AC5-plus-click.png` | Click sul "+" con circolo a 4 membri naviga al form, nessun secondo pulsante "Registra" in pagina |
| AC6 — guardia CTA su circolo <4 membri | ✅ PASS | `screenshots/AC6-guard.png` | Banner "Servono almeno 4 membri..." mostrato, nessuna navigazione al form |
| AC7 — endpoint aggregato single-call | ⏭️ N/A qui | — | Demandato esplicitamente a US-070 per decisione di piano; oggi 2 chiamate REST (debito tecnico documentato) |

## Happy path
`walkthrough.webm` — copre login → dashboard con azioni urgenti + circolo attivo + serie + ultime partite → click "+" → navigazione al form di registrazione partita (circolo con 4 membri).

## Console errors raccolti
Nessuno. Vedi `console.log`.

## Network failures (status >= 400)
Nessuna. Vedi `network.log`.

## Test automatizzati (a supporto)
- `frontend/golp-app/e2e/us-063-dashboard.spec.ts` — 2/2 PASS (circolo pieno con azioni urgenti + guardia AC6 su circolo separato a 2 membri)
- Unit test: 15/15 PASS (`dashboard.utils.spec.ts`, `dashboard.component.spec.ts`)
- Integration test backend: 371/371 PASS (incluso nuovo `GetMyCircles_MultipleCircles_ExposesJoinedAtInJoinOrder`)

## Verdict suggerito al revisore umano
**APPROVE**

Tutti gli AC osservabili sono verificati con screenshot ed e2e verde. Reviewer aveva già dato APPROVE sul codice con 2 Important risolti prima di questa verifica. Nessun errore console o di rete durante il flusso reale. Restano fuori scope per decisione di piano: selettore multi-circolo (US-066), bottom-nav completa condizionale (US-064), endpoint aggregato (US-070) — tutte story separate già nel backlog (EP-009).

---

> **PROSSIMO PASSO:** revisione umana di questo report + degli artefatti. Se OK, l'umano aggiorna lo status a `DONE` in `docs/BACKLOG.md` (o esegue `/eq-approve US-063`). Se serve fix, riapri `/eq-implement US-063`.
