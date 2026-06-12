# Mockup Dashboard — Home post-login

## Direzione visiva
Stesso brutal minimal dell'app: sfondo #0A0A0A, accento arancio #FF5500, Inter Black. Il numero di rating (64px) è l'elemento oversize che dà al giocatore il suo "valore" a colpo d'occhio. La hero card ha una striscia arancio in cima (2px) per ancorare l'identità visiva senza fronzoli. Le card "da confermare" che richiedono azione dell'utente hanno un bordo arancio tenue e un gradiente leggerissimo per distinguersi dalle partite già gestite.

## Schermate
- `index.html` — dashboard principale con: circle selector, hero card (rating + posizione + statistiche), sezione "Da confermare" (2 card), sezione "Partite recenti" (3 card: vittoria, sconfitta, contestata), quick actions

## Interazioni demo
- **Circle selector** (pill in alto): click cicla tra due circoli con dati diversi (hero card si aggiorna)
- **Bottone in basso** "↔ Stato: con conferme in attesa": toglia tra stato con partite da confermare e stato vuoto (empty state)

## Struttura sezioni

### Hero card
Rating grande (64px arancio) + posizione (#N di M) + mini-stats (partite / vittorie / sconfitte)

### Da confermare
- Card con bordo arancio: il giocatore deve ancora confermare → CTA verde "Conferma risultato"
- Card senza CTA: il giocatore ha già confermato, in attesa degli altri → progress dots
- Empty state: check + "Nessuna partita in attesa"

### Partite recenti
- Partita confermata vittoria: delta `+18 pt` verde + badge "VINCE" sul team vincente
- Partita confermata sconfitta: delta `−11 pt` rosso
- Partita contestata: badge rosso "Contestata", nessun delta

### Quick actions
Grid 2×2 con link rapidi: Registra partita · Classifica · Circoli · Statistiche

## Token chiave
- Colori: vedi `style-tokens.json`
- Rating display: `--font-size-rating: 64px`, `font-weight: 900`, `color: accent`
- Delta badge positivo: success su success-bg
- Delta badge negativo: error su error-bg
- Match card "tocca a te": bordo `rgba(255,85,0,0.30)` + gradiente sottile

## Come ispezionarlo
Apri `index.html` nel browser. Usa i controlli interattivi per esplorare gli stati.

---

> **PROSSIMO PASSO:** esegui `/eq-implement US-008` (classifica) o implementa direttamente il componente dashboard usando questi mockup come riferimento visivo.
