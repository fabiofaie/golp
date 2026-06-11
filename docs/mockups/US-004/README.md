# Mockup US-004 — Registra Partita 2v2

## Direzione visiva
Stessa palette GOLP (#0A0A0A / #FF5500 / Inter Black). Le due squadre sono "blocchi avversari" con accent color distinto (arancio Squadra 1 vs azzurro Squadra 2) e un VS divider testuale. Gli input score usano font-weight 900 oversize — i numeri sono il contenuto principale. Nessun ornamento superfluo: il form è usato ogni partita, deve essere veloce.

## Schermate
- `index.html` — Form inserimento partita. Mostra: context badge circolo+sport, 4 player slot (2 per team con indicatore "Tu"), sezione score adattiva (sets=true con add/remove set, oppure score unico per sets=false via toggle demo), validazione errore. Link a `success.html`.
- `success.html` — Conferma post-submit: riepilogo set per set + punteggio vinto, badge "In attesa (1/4)", CTA.

## Interazioni demo (app.js)
- Toggle "Sets / Score unico" per vedere entrambe le varianti senza cambiare sport
- "+ Aggiungi set" / "×" per la lista set dinamica
- Click "Inserisci partita" naviga a `success.html`

## Token chiave
- Colori squadra: `team1` = `#FF5500`, `team2` = `#0EA5E9` — distinti dall'accent principale
- Score input: font-weight 900, `fontSize.score = 40px`, top border colorato per squadra
- Pending badge: `#F59E0B` amber — diverso da success (verde) e error (rosso)
- Vedi `style-tokens.json` (fonte di verità per Engineer)

## Come ispezionarlo
Apri `index.html` nel browser. Usa il toggle in alto per cambiare tra sets / score unico. Il pulsante "Inserisci partita" porta a `success.html`.

---

> **PROSSIMO PASSO:** esegui `/eq-implement US-004` per implementare. L'Engineer leggerà `style-tokens.json` come contratto visivo e questi mockup come riferimento layout.
