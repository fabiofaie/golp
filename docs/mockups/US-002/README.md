# Mockup US-002 — Creazione circolo con configurazione sport

## Direzione visiva

Estende il visual language brutal-minimal di US-001 (dark #0A0A0A, accento #FF5500, Inter Black) con un nuovo pattern a card per i circoli. La lista circoli mostra il rating personale prominente in arancione. I badge per-sport usano colori semantici (padel #FF5500, beach tennis #0EA5E9, basket #F59E0B, burraco #8B5CF6) per rendere il tipo di disciplina leggibile a colpo d'occhio senza dover leggere il testo.

La schermata di creazione include un **config preview interattivo**: non appena si seleziona la disciplina, appare un pannello con le pill "Giochi / Con set / 2v2" — rende immediatamente visibile al giocatore quale sistema di punteggio verrà assegnato senza che debba uscire dalla schermata.

## Schermate

- `index.html` — Lista circoli (MyCircles): stato popolato con 3 card + stato vuoto (toggle in basso a destra)
- `create-circle.html` — Form creazione circolo: selezione nome + disciplina con config preview animata; success state con redirect simulato; bottone "simulate 409" per esplorare l'errore nome duplicato

## Token chiave

- Colori: vedi `style-tokens.json` sezione `colors` (fonte di verità per Engineer)
- Tipografia: vedi `style-tokens.json` sezione `typography`
- Nuovi componenti in `style-tokens.json` sezione `components`: `circleCard`, `sportBadge`, `ratingValue`, `selectWrapper`, `sportPreview`, `pill`
- Sport colors: `sportPadel` `sportBeachtennis` `sportBasket2v2` `sportBurraco`

## Come ispezionarlo

1. Apri `index.html` nel browser — mostra la lista circoli popolata
2. Clicca "toggle empty state" (in basso a destra) per vedere lo stato vuoto
3. Clicca "+ Nuovo circolo" per navigare al form di creazione
4. In `create-circle.html`: seleziona una disciplina per vedere il config preview; compila nome + disciplina per abilitare il submit; premi "simulate 409" per vedere l'errore nome duplicato

---

> **PROSSIMO PASSO:** esegui `/eq-implement US-002` per implementare. L'Engineer leggerà `style-tokens.json` come contratto visivo e questi mockup come riferimento layout.
