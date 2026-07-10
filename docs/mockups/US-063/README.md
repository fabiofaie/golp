# Mockup US-063 — Redesign dashboard con gerarchia azioni urgenti/contesto/personale/storico

## Direzione visiva
Stessa lingua visiva già validata in `docs/mockups/dashboard-v2/` (dark, accento arancione, card con bordo sottile) ma con supporto **tema chiaro/scuro via CSS vars** (obbligatorio per il progetto — vedi `project-dual-theme.md`), e copertura degli stati AC1/AC6 non presenti nel mockup precedente. Un solo CTA primario: il "+" con etichetta "Registra" nella bottom-nav, mai duplicato in header.

## Schermate
- `index.html` — stato pieno: circolo attivo, 2 azioni urgenti (una `pending` arancione, una `disputed` rossa — trattamento visivo distinto), rating/posizione/statistiche, serie vittorie, ultime partite.
- `states.html` — 4 stati edge-case affiancati: sezione azioni urgenti assente (0 richieste), CTA bloccata su circolo con meno di 4 membri, circolo attivo senza partite (serie=0 + empty state), sola contestazione senza pending.

## Token chiave
- Colori: vedi `style-tokens.json` (fonte di verità per Engineer), definiti come CSS custom properties in `shared.css` sotto `:root[data-theme="dark"]` e `:root[data-theme="light"]`.
- Tipografia: Inter, vedi `style-tokens.json`.
- Toggle tema in alto a destra (persistito in `localStorage`, solo nel mockup — nell'app reale seguirà il meccanismo già esistente).

## Come ispezionarlo
Apri `index.html` nel browser. Usa il pulsante ☾/☀ in alto a destra per verificare che nessun colore sia hex hard-coded. Apri `states.html` per gli stati edge-case.

---

> **PROSSIMO PASSO:** esegui `/eq-implement US-063` per implementare. L'Engineer leggerà `style-tokens.json` come contratto visivo e questi mockup come riferimento layout, incluso il trattamento distinto pending/disputed e la guardia sui 4 membri minimi.
