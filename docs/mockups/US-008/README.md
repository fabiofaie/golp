# Mockup US-008 — Classifica Circolo

## Direzione visiva
Dark minimal con tocchi oro/argento/bronzo per il podio. Differenziante: podio a 3 colonne in cima (1° al centro, più alto), seguito dalla lista completa dove la riga del current user ha border-left arancio e background leggermente colorato. Il rating ELO in arancio bold a destra di ogni riga come valore principale.

## Schermate
- `index.html` — classifica completa con podio top-3, lista ranked con current user evidenziato, sezione "Non classificati"

## Token chiave
- Colori: vedi `style-tokens.json`
- Oro: `#F59E0B`, Argento: `#9CA3AF`, Bronzo: `#D97706`
- Current user row: `border-left: 3px solid rgba(255,85,0,0.30)`, bg `rgba(255,85,0,0.06)`
- Rating color: `#FF5500` (accent)

## Interazioni demo
Pulsante "Stato vuoto" mostra il circolo senza partite confirmed (tutti non classificati).

## Come ispezionarlo
Apri `index.html` nel browser. Il current user (Marco, rank 1) ha evidenziazione arancio.

---

> **PROSSIMO PASSO:** esegui `/eq-implement US-008` per implementare. L'Engineer leggerà `style-tokens.json` come contratto visivo e questi mockup come riferimento layout.
