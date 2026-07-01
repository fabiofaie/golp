# Mockup US-040 — Pagina pubblica conferma partita via token

## Direzione visiva

Dark minimal coerente con il design system GOLP (#0A0A0A bg, Inter, #FF5500 accent). La pagina è pubblica (nessun nav/sidebar), layout centrato max 440px. Sensazione "landing" pulita — l'utente vede solo il riepilogo partita e i due bottoni. Header minimale con solo il logo.

## Schermate

- `index.html` — pagina principale con token valido. 5 scenari navigabili:
  - Token valido, partita in attesa → mostra riepilogo + bottoni Conferma/Contesta
  - Token già usato (hai già risposto) → messaggio informativo + stato
  - Partita già confermata da tutti → riepilogo read-only
  - Token scaduto → messaggio con link all'app
  - Token usato (rivisita link) → informativo + link app
- `post-action.html` — stati dopo l'azione. 4 scenari:
  - Confermato (utente attivo) → successo + riepilogo aggiornato
  - Confermato (ospite non attivo) → successo + **CTA "Crea il tuo account GOLP"**
  - Contestato (utente attivo) → warning + info circolo owner notificato
  - Contestato (ospite non attivo) → warning + CTA register

## Token chiave

- Colori: vedi `style-tokens.json`
- Background: `--color-bg: #0A0A0A`
- Accent: `--color-accent: #FF5500`
- Team1 (arancio): `--color-team1: #FF5500`
- Team2 (azzurro): `--color-team2: #0EA5E9`
- Confirm dot verde: `--color-success: #22C55E`
- Dot "sei tu" arancio: `--color-accent`
- Guest badge giallo: `--color-ghost: #F59E0B`

## Come ispezionarlo

Apri `index.html` nel browser. Usa i pulsanti nella scenario bar per navigare tra i 5 stati. Apri `post-action.html` per vedere i 4 stati post-azione.

---

> **PROSSIMO PASSO:** esegui `/eq-implement US-040` per implementare. L'Engineer leggerà `style-tokens.json` come contratto visivo e questi mockup come riferimento layout.
