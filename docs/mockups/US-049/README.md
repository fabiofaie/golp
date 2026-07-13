# Mockup US-049 — Raduno al circolo: suggerimento accoppiamenti per i presenti

## Direzione visiva
Stessa palette dark neutra + accent `#FF5500` già consolidata in `docs/mockups/US-066/`, per coerenza con l'app. Differenziante: i presenti si selezionano come chip-avatar tappabili (roster da spogliatoio), la proposta appare come card "campo" con squadre contrapposte e swap manuale tap-to-swap prima di confermare.

## Schermate
- `index.html` — unica schermata, stato gestito via JS: griglia check-in membri → stepper "Campi disponibili" + toggle "Totale raduno / A testa" con stepper valore obiettivo → (da 4 presenti) CTA "Genera piano" → piano multi-turno con tab "Turno 1/2/3...", ogni turno mostra fino a N round card (una per campo) con VS, swap giocatori, resting-strip per quel turno, bottone "Registra questa partita" per campo (simulato: mostra toast, nella UI reale naviga a `record-match` precompilato).

## Token chiave
- Colori: vedi `style-tokens.json` (fonte di verità per Engineer)
- Tipografia: vedi `style-tokens.json`

## Come ispezionarlo
Apri `index.html` nel browser. Seleziona almeno 4 chip (o aggiungi un ospite), premi "Genera proposta", prova a scambiare giocatori tra le squadre tappando due nomi nello stesso campo.

## Note per l'implementazione
- Il mockup simula il matchmaking con uno shuffle+bilanciamento rating semplificato: la logica reale (combinazioni meno giocate + tie-break su `RatingMethod` del circolo) è nel piano `docs/planning/US-049.md`, non in questo JS.
- Il tasto "Registra questa partita" nel mockup mostra solo un toast: nell'app reale naviga al form di registrazione partita esistente, precompilato con i 4 giocatori del campo.

---

> **PROSSIMO PASSO:** esegui `/eq-implement US-049` per implementare. L'Engineer leggerà `style-tokens.json` come contratto visivo e questi mockup come riferimento layout.
