# Mockup US-044 — Storico partite personale nella dashboard

## Direzione visiva

Stesso linguaggio del dashboard mockup esistente: dark, Inter, accent arancio `#FF5500`. La sezione partite vive in una **pagina dedicata** (non embedded nella dashboard): la dashboard aggiunge solo un pulsante "📋 Le mie partite". La pagina dedicata mostra il segmented control filter + lista compatta + load-more. Elemento differenziante: il delta ELO è il dato più grande e colorato — verde/rosso a colpo d'occhio. Card con bordo sinistro colorato per status win/loss/pending/disputed.

## Schermate

- `index.html` — dashboard con pulsante "Le mie partite" tra i quick nav link
- `matches.html` — pagina dedicata con header back + filter tabs + lista paginata

## Interazione simulata

- Dashboard: click "Le mie partite" → naviga a `matches.html`
- Matches: click tab filter → lista si aggiorna con solo le partite del tipo selezionato
- Matches: click "Carica altre" → appende le successive (PAGE_SIZE = 5)
- Status `pending`/`disputed` → delta mostra "—"

## Token chiave

- Colori, tipografia, spacing: vedi `style-tokens.json` (fonte di verità per l'Engineer)
- `matchRow.borderLeftColors` — il bordo sinistro cambia colore per win/loss/pending/disputed
- `deltaValue` — font-size `lg` (18px), weight `black` (900) — il numero più grande della riga

## Come ispezionarlo

Apri `index.html` nel browser. Click "Le mie partite" per navigare a `matches.html`.

---

> **PROSSIMO PASSO:** esegui `/eq-implement US-044` per implementare. L'Engineer leggerà `style-tokens.json` come contratto visivo e questi mockup come riferimento layout.
