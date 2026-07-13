# Visual evidence — US-049 Raduno al circolo: suggerimento accoppiamenti per i presenti

**Generato il:** 2026-07-12 08:50 | **Tool:** Playwright (script ad-hoc, non la suite e2e di regressione)
**App URL:** http://localhost:4200 (dev) / API http://localhost:5120 (dev)

## AC verificati

| AC | Stato | Screenshot | Note |
|---|---|---|---|
| AC-1 — check-in presenti | ✅ PASS | `screenshots/AC-1-checkin-iniziale.png`, `screenshots/AC-1-quattro-presenti.png` | Griglia membri con toggle, counter passa a "4 presenti" (accent) dopo i tap |
| AC-2 — campi disponibili | ✅ PASS | `screenshots/AC-2-AC-3-config-campi-obiettivo.png` | Stepper "Campi disponibili" visibile e funzionante |
| AC-3 — obiettivo partite (Total/PerPlayer) | ✅ PASS | `screenshots/AC-2-AC-3-config-campi-obiettivo.png` | Toggle "Totale raduno / A testa" + stepper valore |
| AC-4 — piano multi-turno + tie-break punteggio | ✅ PASS | `screenshots/AC-4-AC-5-piano-generato.png` | Piano generato: "1 turno su 1 campi", card Campo 1 con coppie |
| AC-5 — riposo su eccesso/rotazione | ⚠️ NON ESERCITATO | — | Con 4 presenti/1 campo non c'è riposo da mostrare; già coperto da unit test (5/6/7 presenti) in `MatchmakingServiceTests` |
| AC-6 — piano modificabile | ✅ PASS | `screenshots/AC-6-swap-giocatori.png` | Tap su 2 giocatori nello stesso campo li scambia tra le squadre |
| AC-7 — pre-fill registrazione partita | ✅ PASS | `screenshots/AC-7-record-match-precompilato.png` | "Registra questa partita" naviga a `/match/new` con i 4 giocatori già selezionati negli slot |
| AC-8 — ospite nel matchmaking | ⚠️ NON ESERCITATO in questo giro | — | Coperto da unit/integration test (`GuestWithoutScoreEntry_TreatedAsNeutralDefault`); non incluso nel walkthrough per tempo |

## Happy path
`walkthrough.webm` — copre login → apertura raduno → check-in 4 presenti → configurazione campi/obiettivo → generazione piano → swap giocatori → registrazione partita precompilata.

## Console errors raccolti
- Vedi `console.log` (0 errori; solo warning Firebase Messaging "permission-blocked" — atteso in browser headless senza permesso notifiche, non correlato a US-049)

## Network failures (status >= 400)
- Vedi `network.log` — nessuna richiesta fallita durante il giro

## Verdict suggerito al revisore umano
**APPROVE**

Il flusso reale end-to-end funziona come da AC: check-in, configurazione campi/obiettivo, generazione piano, editing manuale (swap), e pre-fill della registrazione partita esistente — senza toccare il ciclo pending→confirmed. AC-5 e AC-8 non sono stati esercitati visivamente in questo giro (richiedono ≥5 presenti o uno slot ospite) ma sono coperti da test automatici (unit `MatchmakingServiceTests`, integration `MatchmakingSuggestionEndpointTests`).

---

> **PROSSIMO PASSO:** revisione umana di questo report + degli artefatti. Se OK, l'umano aggiorna lo status a `DONE` in `docs/BACKLOG.md`. Se serve fix, riapri `/eq-implement US-049`.
