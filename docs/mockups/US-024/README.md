# Mockup US-024 — Guida all'installazione della PWA

## Direzione visiva
Riusa il dark theme esistente di GOLP (bg `#0A0A0A`, accent senape-arancio `#FF5500`, Inter) — nessuna nuova palette. Banner come bottom-sheet flottante non invasivo; guida come modal full-screen in stile mobile-first con step numerati.

## Differenziante
Badge "Rilevato: OS · Browser" in alto alla guida — comunica subito che le istruzioni sono su misura, non un copia-incolla generico.

## Schermate
- `index.html` — dashboard con banner di invito installazione in basso (stato base, sempre visibile in questo mockup per demo)
- `guide.html` — guida a step con tab di switch tra le 4 combinazioni OS/browser previste (iOS Safari, Android Chrome, Android Samsung Internet, fallback generico) per dimostrare il branching del contenuto

## Token chiave
- Colori, font, spacing, radius: vedi `style-tokens.json` (fonte di verità per Engineer)
- Estende i token esistenti (`docs/mockups/US-001/style-tokens.json`) solo con i token specifici della guida install (badge, numero step)

## Come ispezionarlo
Apri `index.html` nel browser. Clicca "Scopri come" per andare a `guide.html`, poi usa i tab in alto per vedere le 4 varianti di contenuto.

**Nota:** in `index.html` il banner è sempre visibile per scopo demo. Nell'app reale appare solo al primo accesso da browser mobile non-standalone (logica in `PwaInstallService`, non in questo mockup). In `guide.html` i tab simulano la detection automatica — nell'app reale il tab corretto è già selezionato senza scelta utente.

---

> **PROSSIMO PASSO:** esegui `/eq-implement US-024` per implementare. L'Engineer leggerà `style-tokens.json` come contratto visivo e questi mockup come riferimento layout.
