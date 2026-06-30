# Mockup US-039 — Giocatore ospite in una partita

## Direzione visiva

Stessa visual language del sistema GOLP (dark #0A0A0A, Inter, accent #FF5500). Elemento differenziante: ogni slot giocatore è una "player card" con **toggle pill `[Membro | Ospite]`** — in modalità Ospite si apre una sezione con campi nome + email/telefono + pulsante Contact Picker (amber #F59E0B come colore ghost). Il badge `(non registrato)` usa lo stesso amber per coerenza con lo stato ghost negli elenchi.

## Schermate

- `index.html` — form "Registra Partita" con slot togglabili. Usa la barra scenari in alto per vedere: form vuoto, slot ospite aperto, ospite compilato, Contact Picker disponibile, stato misto.
- `history.html` — storico partite con badge `non registrato` accanto ai nomi dei giocatori ghost, sia in partite pending che confirmed.

## Interazioni mockup

- **Toggle [Membro | Ospite]**: cliccabile su ogni slot (tranne slot 1, bloccato su Membro = il registratore)
- **Barra scenari** (top): 5 preset che mostrano gli stati principali
- **Pulsante "Scegli dai contatti"**: visibile solo quando Contact Picker è abilitato (scenario 4-5); cliccando precompila nome+telefono con dati mock simulati

## Token chiave

- Colori ghost (ospite/non-registrato): `#F59E0B` su `rgba(245,158,11,0.10)` — vedi `style-tokens.json`
- Colori Contact Picker: `#8B5CF6` (viola) — distinto da accent per segnalare azione "esterna"
- Toggle pill attivo-ospite: amber `#F59E0B` con testo nero (leggibilità)
- Tutti i token base ereditati da US-037 (dark, team1 orange, team2 sky)

## Come ispezionarlo

1. Apri `index.html` nel browser
2. Usa la barra scenari in alto per navigare tra gli stati
3. Clicca i toggle `[Membro | Ospite]` liberamente
4. Apri `history.html` per il badge `(non registrato)` nel contesto dello storico

---

> **PROSSIMO PASSO:** esegui `/eq-implement US-039` per implementare. L'Engineer leggerà `style-tokens.json` come contratto visivo e questi mockup come riferimento layout.
