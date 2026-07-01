# Mockup US-041 — Quick Match

## Direzione visiva
Continuità totale col design system esistente (dark #0A0A0A, Inter, accent #FF5500).
Elemento differenziante: chip cloud per i giocatori suggeriti — avatar tappabili a griglia 3 colonne invece di dropdown. I colori team (arancio / blu) si attivano sugli slot appena si assegna un giocatore. Stepper numerico persistente comunica sempre a che passo sei nel flusso.

## Schermate
- `index.html` — Step 1: scelta sport (sport cards 2×2, Padel pre-selezionato)
- `players.html` — Step 2: selezione giocatori (stato parzialmente riempito: Tu + Anna in squadra A, 2 slot vuoti in squadra B, chip cloud con suggeriti)
- `circle-picker.html` — Interstitial: picker quando il sistema trova più circoli con gli stessi 4 giocatori per lo stesso sport
- `score.html` — Step 3+4: risultato (set row) + toggle mockup "circolo esistente / nuovo gruppo" per vedere entrambe le varianti

## Decisioni UX codificate nel mockup
- **Slot "Tu" auto-filled** e non removibile (badge "Tu", sfondo arancio tenue)
- **Chip usati** → opacità 35%, non cliccabili (Anna già in squadra A)
- **1 circolo match** → redirect silenzioso (info banner in score.html, senza picker)
- **N circoli match** → picker esplicito (circle-picker.html) con nome + data ultima partita
- **0 circoli** → sezione "Nome gruppo" appare in score.html dopo il risultato, pre-compilata
- **Ospiti** → chip con bordo tratteggiato + label "ospite"; slot con bordo ambra

## Token chiave
- Colori: vedi `style-tokens.json` (fonte di verità per Engineer)
- Tipografia: Inter, stessa scala di US-039

## Come ispezionarlo
Apri `index.html` nel browser. Naviga: Sport → Giocatori → (circle-picker) → Risultato.
In `players.html` clicca i chip per riempire gli slot e sbloccare il bottone "Avanti".
In `score.html` usa il toggle "Circolo esistente / Nuovo gruppo" per vedere entrambe le varianti.

---

> **PROSSIMO PASSO:** esegui `/eq-implement US-041` per implementare. L'Engineer leggerà `style-tokens.json` come contratto visivo e questi mockup come riferimento layout.
