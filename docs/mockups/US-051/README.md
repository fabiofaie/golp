# Mockup US-051 — Scelta metodo di calcolo punteggio del circolo (ELO vs Game+Bonus)

## Direzione visiva

Stesso linguaggio dark degli altri mockup GOLP (bg `#0A0A0A`, accent arancio `#FF5500`, Inter). Due card grandi selezionabili (radio-style) invece di un dropdown anonimo: ogni card mostra un esempio concreto del calcolo così l'owner capisce la differenza prima di scegliere, senza dover leggere una spiegazione lunga. I due parametri (N partite / M settimane) compaiono solo sotto la card "Game+Bonus", e solo se è quella selezionata — coerente con l'AC "i parametri sono visibili solo se il metodo attivo è Game+Bonus".

## Schermate

- `index.html` — sezione impostazioni circolo con selettore metodo, parametri finestra condizionali, pulsante salva, e una preview della classifica con etichetta del metodo attivo (AC "la classifica mostra un'etichetta che indica quale metodo è attivo")

## Interazione simulata

- Click su una card → la seleziona (bordo arancio + radio pieno), deseleziona l'altra
- Selezione "Game+Bonus" → mostra i due input numerici (N partite, M settimane)
- Selezione "ELO" → nasconde i due input
- Cambio metodo aggiorna live il badge e il suffisso punti nella preview classifica (puramente illustrativo, nel prodotto reale richiede salvataggio)
- Click "Salva configurazione" → mostra un check "✓ Salvato" per 1.8s (simulato, nessuna chiamata reale)

## Token chiave

- Colori, tipografia, spacing: vedi `style-tokens.json` (fonte di verità per l'Engineer)
- `methodCard` — stato selezionato = bordo 2px arancio + sfondo `accent-dim`, stato normale = bordo 1px `border`
- `windowParams` — grid 2 colonne, nascosto via classe `.hidden` quando il metodo non è Game+Bonus
- `methodBadge` — pill piccola arancio usata nella preview classifica per indicare il metodo attivo

## Come ispezionarlo

Apri `index.html` nel browser. Clicca sulle due card per vedere mostrarsi/nascondersi i parametri e cambiare il badge della classifica.

---

> **PROSSIMO PASSO:** esegui `/eq-implement US-051` per implementare. L'Engineer leggerà `style-tokens.json` come contratto visivo e questi mockup come riferimento layout.
