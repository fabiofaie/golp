# Mockup my-circles-buttons — Pulsanti card circolo (pagina /circles)

## Direzione visiva

Stesso dark mode e palette esistenti (`--color-bg: #0A0A0A`, accent `#FF5500`, font Inter) — nessuna rottura del design system. Il problema non era la palette ma la **gerarchia**: 6 pulsanti identici (stesso stile ghost) rendevano impossibile capire a colpo d'occhio quale fosse l'azione principale e quali fossero riservate al proprietario.

Tre varianti invece di una:
- **`btn-primary`** (riempito, accent) — solo "Registra partita", l'azione a maggior frequenza.
- **`btn-nav`** (outline neutro + icona) — Classifica, Partite, Premi, Stats: stesso peso, sono tutte navigazione verso vista dati.
- **`btn-owner`** (bordo tratteggiato accent) — Invita, + Giocatore: segnala visivamente "solo tu le vedi", separate da un divider con etichetta "Solo proprietario".

## Elemento differenziante

Icone inline (stroke, 14px) su ogni pulsante — oggi solo testo — più il divider etichettato che separa le azioni del membro da quelle dell'owner, eliminando l'ambiguità "perché questi 2 pulsanti sono diversi dagli altri 4".

## Schermate

- `index.html` — card circolo isolata, stesso contesto della pagina `/circles` (titolo + sottotitolo pagina inclusi per coerenza, il resto della lista circoli non è mockato)

## Token chiave

- Colori: vedi `style-tokens.json` (fonte di verità)
- 3 varianti pulsante documentate in `style-tokens.json` → `buttonVariants`
- Spacing pulsanti: gap 8px tra pulsanti, 10px tra righe

## Come ispezionarlo

Apri `index.html` nel browser. Nessuna interazione richiesta (hover-only, nessun JS).

## Nota per l'implementazione

Il componente reale è `frontend/golp-app/src/app/circles/my-circles/my-circles.component.html` — oggi tutti i 6 pulsanti usano la classe `.btn-ghost` con stile inline. La migrazione a 3 classi (`.btn-primary`, `.btn-nav`, `.btn-owner`) va fatta in `styles.scss` (dove vivono già `--color-*` token), riusando le variabili esistenti invece di quelle hardcoded qui nel mockup.

---

> **PROSSIMO PASSO:** non è legato a una user story esistente (esplorazione UI libera). Per applicarlo al codice: descrivi a Claude "applica il mockup my-circles-buttons al componente my-circles" — non esiste uno US-XXX dedicato, quindi `/eq-implement` non si applica direttamente. Se vuoi tracciarlo formalmente, prima `/eq-spec --add "Migliorare gerarchia visiva pulsanti azione circolo (vedi mockup my-circles-buttons)"`.
