# Mockup US-009 — Storico Partite con Delta Punti

## Direzione visiva
Dark brutal minimal, continuità con US-005. Differenziante: la colonna delta a destra di ogni card mostra `+12` / `−8` / `+0` in tipografia black 28px, con colori semantici verde/rosso/grigio. Per partite pending o contestate, tre puntini verticali indicano assenza di delta senza confondere. Tab bar compatta per filtrare per stato.

## Schermate
- `index.html` — lista partite del circolo con tab filter (Tutte / Confermate / In attesa / Contestate), 5 card campione che coprono tutti gli stati

## Token chiave
- Colori: vedi `style-tokens.json` (fonte di verità per Engineer)
- Tipografia: Inter Black 28px per `--font-size-delta` (delta value hero)
- Delta positivo: `#22C55E`, negativo: `#FF4444`, zero/assente: `#9A9A9A`

## Interazioni demo
Il tab bar filtra le card in-page. Il pulsante stato vuoto mostra l'empty state.

## Come ispezionarlo
Apri `index.html` nel browser. Clicca i tab per filtrare. Nessuna dipendenza esterna oltre Google Fonts.

---

> **PROSSIMO PASSO:** esegui `/eq-implement US-009` per implementare. L'Engineer leggerà `style-tokens.json` come contratto visivo e questi mockup come riferimento layout.
