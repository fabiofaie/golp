# Visual evidence — US-003 Iscrizione a uno o più circoli

**Generato il:** 2026-06-11 | **Tool:** Playwright (headless Chromium)
**App URL:** http://localhost:4200 (dev) | Backend: http://localhost:5120

## AC verificati

| AC | Stato | Screenshot | Note |
|---|---|---|---|
| AC1 — Join circolo + appare in lista | ✅ PASS | `screenshots/AC1-browse-page.png`, `AC1-before-join.png`, `AC1-after-join-member-badge.png` | Badge "Membro" visibile dopo click "Unisciti" |
| AC2 — Multi-circolo | ✅ PASS | `screenshots/AC2-two-circles.png` | My Circles mostra 2 card dopo join + creazione secondo circolo |
| AC3 — Rating 1000 al join | ✅ PASS | `screenshots/AC3-my-circles-rating-1000.png` | `.rating-value` contiene "1000" per circolo appena joinato |
| AC4 — Doppia iscrizione idempotente | ✅ PASS | `screenshots/AC4-already-member-badge.png` | Nessun pulsante join, solo badge "Membro" |
| AC5 — Isolamento lista membri | ✅ PASS | `screenshots/AC5-isolated-circles.png` | My Circles mostra solo i circoli dell'utente corrente |

## Happy path

Video registrato in `test-results/` per ogni test (Playwright `video: 'on'`).

## Console errors raccolti

Vedi `console.log` — nessun errore/warning console durante i test.

## Network failures (status >= 400)

Vedi `network.log` — nessuna richiesta API fallita durante i test.

## Note tecniche

**Bug trovato e risolto durante la verifica:**
`proxy.conf.json` usava il pattern `/circles` come prefisso greedy, intercettando ANCHE le rotte Angular `/circles/browse`, `/circles/new`, `/circles` (SPA). Un `page.goto('/circles/browse')` riceveva una risposta 404 dal backend invece dell'`index.html` Angular → pagina bianca.

**Fix applicati:**
1. `e2e/verify-us-003.spec.ts`: usa navigazione SPA (click su `routerLink`) invece di `page.goto()` per le rotte sotto `/circles/*`.
2. `proxy.conf.js` (nuovo): usa funzione `context` per distinguere navigazioni browser (`Accept: text/html` → non proxied → Angular SPA) da chiamate API (`Accept: application/json` → proxied → backend).

Riavviare il dev server con `ng serve --proxy-config proxy.conf.js` per attivare il fix del proxy.

## Verdict suggerito al revisore umano

**APPROVE**

Tutti e 5 gli AC sono verificati con screenshot e test verde. Nessun errore console, nessuna richiesta fallita. La logica di join, rating iniziale, idempotenza e isolamento funzionano correttamente.

---

> **PROSSIMO PASSO:** revisione umana di questo report + degli artefatti. Se OK, aggiorna manualmente lo status a `DONE` in `docs/BACKLOG.md`. Se serve fix, riapri `/eq-implement US-003`.
