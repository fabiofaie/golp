# Mockup US-037 — Pagina di dettaglio partita

## Direzione visiva
Stesso linguaggio di US-005 (dark, accent arancio FF5500, Inter black per i numeri).
Nessuna palette nuova: estensione dei token esistenti con delta ELO (verde/rosso) e badge "Forzata" (viola, distintivo per la forzatura proprietario).

## Schermate
- `index.html` — pagina dettaglio read-only con 4 stati condizionali selezionabili dal toggle demo in fondo: confermata-da-giocatore, confermata-da-forzatura, pending, disputed.

## Token chiave
- Colori: vedi `style-tokens.json` (fonte di verità per Engineer)
- Tipografia: vedi `style-tokens.json`
- Nuovi componenti rispetto a US-005: `decisionStrip`, `deltaChip`, `forcedBadge`, `playerDeltaRow`

## Come ispezionarlo
Apri `index.html` nel browser. Usa i 4 bottoni in fondo alla pagina per passare tra gli stati.

---

> **PROSSIMO PASSO:** esegui `/eq-implement US-037` per implementare. L'Engineer leggerà `style-tokens.json` come contratto visivo e questi mockup come riferimento layout.
