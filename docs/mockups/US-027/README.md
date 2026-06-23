# Mockup US-027 — Palette colori tema chiaro con contrasto verificato

## Direzione visiva
Nessuna reinvenzione: stessa griglia, tipografia (Inter) e nomi componente del prodotto esistente (`circle-card`, `sport-badge`, `match-card`, `delta-badge`, ecc.), solo palette invertita su base chiara con i toni semantici (oro/argento/bronzo, partner/avversario, sport, stati) scuriti dove serve per restare leggibili su sfondo bianco/crema invece che su quasi-nero. Bianco crema (`#F7F5F2`) invece di bianco puro per restare meno clinico.

## Schermate
- `index.html` — galleria comparativa a due colonne: stessi componenti (card circolo, classifica con medaglie, statistiche partner/avversario, card partita con stati pending/confirmed/disputed, badge delta rating, pillole sport, bottone primario) renderizzati prima con la palette scura attuale, poi con la proposta chiara.

## Token chiave
- Colori: vedi `style-tokens.json`, sezioni `themes.dark` (attuale, da `styles.scss`) e `themes.light` (proposta)
- Note di contrasto: campo `contrast_notes` in `style-tokens.json`

## Come ispezionarlo
Apri `index.html` nel browser. Nessuna interazione: è un confronto statico fianco a fianco.

---

> **PROSSIMO PASSO:** esegui `/eq-implement US-027` per implementare. L'Engineer leggerà `style-tokens.json` come contratto visivo (valori `themes.light`) e applicherà i token dentro `:root.theme-light` in `frontend/golp-app/src/styles.scss`, secondo il piano in `docs/planning/US-027.md`.
