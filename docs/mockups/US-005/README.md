# Mockup US-005 — Conferma collettiva del risultato (4/4)

## Direzione visiva
Estensione del linguaggio dark minimal di US-001→US-004. Palette invariata (bg #0A0A0A, accent orange #FF5500, team1 orange / team2 blue). Elemento differenziante: **4 dot progressivi** per il consensus collettivo — dot verde = confermato, dot arancio = "tu", dot grigio = in attesa. Lettura immediata dello stato 1/4 → 4/4 senza testo verboso.

Pulsanti azione: `Conferma` in verde pieno (#22C55E), `Contesta` in rosso secondario (outline) — gerarchia visiva chiara tra azione primaria e distruttiva.

## Schermate

- `index.html` — Lista partite pending del circolo. Due stati demo:
  - Card 1: user non ha ancora confermato → bottoni `Contesta` / `Conferma` attivi
  - Card 2: user ha già confermato → badge "Hai confermato", in attesa degli altri
  - Stato vuoto togglabile via pulsante demo
- `feedback.html` — Stato post-azione con tab switcher demo:
  - Tab "Confermata (4/4)": recap partita, 4 dot verdi, delta rating (`+14 pt`)
  - Tab "Contestata": messaggio esclusione classifica, badge rosso

## Token chiave
- Colori: vedi `style-tokens.json` (fonte di verità per Engineer)
- Tipografia: vedi `style-tokens.json`
- Nuovi componenti: `confirmDots`, `btnConfirm`, `btnDispute`, `statusBadge`

## Come ispezionarlo
Apri `index.html` nel browser. Clicca "Conferma" o "Contesta" per vedere il modal e l'interazione. Naviga a `feedback.html` per gli stati post-azione.

---

> **PROSSIMO PASSO:** esegui `/eq-implement US-005` per implementare. L'Engineer leggerà `style-tokens.json` come contratto visivo e questi mockup come riferimento layout.
