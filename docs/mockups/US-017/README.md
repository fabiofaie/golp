# Mockup US-017 — Pagina spiegazione ELO e simulatore partita

## Direzione visiva
Dark sport theme GOLP invariato (bg #0A0A0A, accent arancio #FF5500). Il `bg-deco` mostra "ΔR" invece del solito "1000" — rimarca il concetto di variazione rating, coerente con il contenuto della pagina. La spiegazione usa "stat cards" numeriche scannable (4 card in 2 colonne) invece di paragrafi, rendendo i concetti chiave immediatamente leggibili. Il form simulatore usa un pill switcher compatto per il toggle modalità.

## Schermate
- `index.html` — pagina unica: spiegazione ELO (stat cards + 3 blocchi testuali) + form simulatore (rating 4 giocatori, toggle Risultato unico / Per set, calcolo e risultato inline)

## Interazioni (app.js — mockup only)
- **Toggle modalità risultato:** pill switcher Risultato unico ↔ Per set
- **Per set:** pulsante "+ Aggiungi set" aggiunge righe; "×" rimuove (minimo 1)
- **Submit form:** calcola delta ELO con formula locale (replica per il mockup), mostra panel risultato con +/- per ogni giocatore
- **Nota:** in produzione il calcolo avverrà via `POST /simulate-match` (backend), non client-side

## Token chiave
- Colori e tipografia: vedi `style-tokens.json` (fonte di verità per Engineer)
- Nuovi token specifici di questa pagina: `deltaPositive` (#22C55E), `deltaNegative` (#FF4444), `pillSwitcherBg` (#1F1F1F), `pillActiveBg` (#FF5500)
- `brandElement.value`: "ΔR" (invece di "1000")

## Come ispezionarlo
Apri `index.html` nel browser. Compila i 4 campi rating e il risultato, premi "Calcola variazione rating". Testa il toggle modalità per vedere la vista "Per set" con aggiunta/rimozione set dinamica.

---

> **PROSSIMO PASSO:** esegui `/eq-implement US-017` per implementare. L'Engineer leggerà `style-tokens.json` come contratto visivo e questo mockup come riferimento layout. Ricorda: la formula ELO nel mockup (`app.js`) è solo per preview — l'implementazione chiama `POST /simulate-match`.
