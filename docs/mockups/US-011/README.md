# Mockup US-011 — Statistiche Personali

## Direzione visiva
Due card a piena larghezza, palette semantica: verde per il compagno (vittoria = positivo), rosso per l'avversario ostico (difficile = pericolo). Differenziante: anello win-rate CSS-only con `conic-gradient` — il cerchio si riempie proporzionalmente alla percentuale di vittorie, senza SVG né canvas. Percentuale in bold al centro dell'anello. Tre stati gestiti in-page: dati completi, dati parziali (solo uno dei due), nessun dato.

## Schermate
- `index.html` — due card stat con anello win-rate, toggle demo per 3 stati

## Token chiave
- Colori: vedi `style-tokens.json`
- Partner ring: `#22C55E`, track `rgba(34,197,94,0.15)`
- Opponent ring: `#FF4444`, track `rgba(255,68,68,0.15)`
- Ring size: 88px, inner circle: 72px
- `--pct` CSS var controlla l'angolo del conic-gradient (es. 75% = 270deg)

## Interazioni demo
Tre pulsanti: con dati completi, dati parziali (solo compagno), nessuna partita.

## Come ispezionarlo
Apri `index.html` nel browser. L'anello verde mostra 75% per il compagno, quello rosso 20% per l'avversario.

---

> **PROSSIMO PASSO:** esegui `/eq-implement US-011` per implementare. L'Engineer leggerà `style-tokens.json` come contratto visivo e questi mockup come riferimento layout.
