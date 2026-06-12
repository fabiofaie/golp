# Mockup US-010 — Giocatore del Mese e dell'Anno

## Direzione visiva
Due card a piena larghezza impilate verticalmente, con palette distinta: oro/ambra per il mese, blu cielo per l'anno. Differenziante: numero decorativo oversize (96px, opacity 8%) nell'angolo in basso a destra di ogni card — crea profondità senza interferire con il contenuto. Nome del vincitore in tipografia black 46px come hero. Stato vuoto gestito direttamente nella card (no schermata separata).

## Schermate
- `index.html` — due card premio, con toggle demo per stato vuoto

## Token chiave
- Colori: vedi `style-tokens.json`
- Mese: `#F59E0B` (gold), bg `rgba(245,158,11,0.08)`, shadow `rgba(245,158,11,0.20)`
- Anno: `#0EA5E9` (year), bg `rgba(14,165,233,0.08)`, shadow `rgba(14,165,233,0.20)`
- Net gain: `#22C55E`
- Nome vincitore: `font-size: 46px`, `font-weight: 900`

## Interazioni demo
Due pulsanti sotto le card per alternare tra stato con vincitori e stato vuoto.

## Come ispezionarlo
Apri `index.html` nel browser. Usa i pulsanti demo per vedere i due stati.

---

> **PROSSIMO PASSO:** esegui `/eq-implement US-010` per implementare. L'Engineer leggerà `style-tokens.json` come contratto visivo e questi mockup come riferimento layout.
