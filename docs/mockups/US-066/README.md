# Mockup US-066 — Selettore circolo attivo e filtro dei contenuti dashboard

## Direzione visiva
Dark system coerente con `docs/mockups/dashboard/` (bg #0A0A0A, accent arancio #FF5500). Il selettore è un bottom-sheet che scivola dal basso, overlay scuro dietro. Righe circolo con swatch colorato per sport, rating/posizione a destra, checkmark sulla riga selezionata. Stelle gialle per i preferiti quando i circoli superano la soglia.

## Schermate
- `index.html` — dashboard con pannello aperto, 3 circoli (nessuna ricerca, nessun raggruppamento preferiti)
- `many-circles.html` — pannello con 8 circoli: campo ricerca in alto, righe raggruppate "Preferiti" / "Altri circoli", stelle interattive

Toggle in alto (demo-only, non parte della UI reale) per passare tra i due stati.

## Token chiave
- Colori, spacing, tipografia: vedi `style-tokens.json` (fonte di verità per Developer)
- `behavior_notes` in `style-tokens.json` descrive soglie (ricerca >5 circoli) e trigger di apertura/chiusura

## Come ispezionarlo
Apri `index.html` nel browser. Click sul bottone "Circolo attivo" o sull'overlay per chiudere/riaprire il pannello (JS in `app.js`, solo demo interattiva). Click su una riga per selezionarla; click sulla stella per preferiti in `many-circles.html`; digita nel campo ricerca per filtrare.

---

> **PROSSIMO PASSO:** esegui `/eq-implement US-066` per implementare. L'Engineer leggerà `style-tokens.json` come contratto visivo e questi mockup come riferimento layout.
