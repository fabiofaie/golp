# Mockup US-003 — Iscrizione a uno o più circoli

## Direzione visiva
Stessa visual language di US-001 e US-002: brutal minimal, dark, Inter, accent arancione #FF5500.
Nuovi componenti: `circle-join-card` (variante con pulsante join e badge "✓ Membro") e `member-row` per la classifica membri. Nessuna deviazione dal design system — coerenza totale con gli schermi precedenti.

## Schermate
- `index.html` — Scopri Circoli: lista circoli con sport filter tabs, pulsante "Unisciti" per circoli non joined, badge "✓ Membro" per circoli già joinati, interazione join simulata (loading → badge + toast)
- `members.html` — Classifica Membri circolo: lista ordinata per rating con rank, avatar, nome, data iscrizione; utente corrente evidenziato in arancione con sfondo tinted

## Token chiave
- Colori: vedi `style-tokens.json` (fonte di verità per Engineer)
- Estensioni US-003: `memberBadgeBg/Border/Text`, `selfHighlight`, componenti `joinButton`, `memberBadge`, `memberRow`, `filterTab`
- Tipografia: invariata da US-002

## Come ispezionarlo
Apri `index.html` nel browser. Clicca "Unisciti" per simulare il join (animazione → badge "Membro" + toast). Usa i tab sport per filtrare. Link "Vedi classifica →" apre `members.html`.

---

> **PROSSIMO PASSO:** esegui `/eq-implement US-003` per implementare. L'Engineer leggerà `style-tokens.json` come contratto visivo e questi mockup come riferimento layout.
