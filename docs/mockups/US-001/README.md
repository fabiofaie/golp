# Mockup US-001 — Registrazione e accesso account giocatore

## Direzione visiva

Dark sport: sfondo quasi-nero (#0A0A0A), accento arancio elettrico (#FF5500), tipografia Inter bold. L'interfaccia punta su contrasto estremo e gerarchia tipografica netta — coerente con un'app dove il dato numerico è protagonista. Niente gradienti, niente decorazioni superflue.

Elemento differenziante: il numero **1000** (rating di partenza) campeggia in semi-trasparenza sul lato destro di ogni schermata auth. Comunica il cuore del prodotto senza una parola di spiegazione.

## Schermate

| File | Schermata | Note demo |
|---|---|---|
| `index.html` | **Login** | Clicca "Accedi" per alternare stato errore ↔ normale |
| `register.html` | **Registrazione** | Digita nella password per vedere la strength bar; clicca "Registrati" per simulare email duplicata |
| `forgot-password.html` | **Recupera password** | Clicca il pulsante per vedere lo stato di successo post-invio |
| `reset-password.html` | **Nuova password** | Barra in basso per passare tra: token valido / token scaduto / successo |

## Token chiave

- Colori: vedi `style-tokens.json` → `colors`
- Tipografia: `Inter` 400/500/700/900, scale in `style-tokens.json` → `typography`
- Spacing: base 4px, scale in `style-tokens.json` → `spacing`
- Componente `bg-deco`: documentato in `style-tokens.json` → `brandElement`

## Come ispezionarlo

Apri `index.html` nel browser. I link tra le schermate funzionano tutti. Ogni pagina ha interazioni demo integrate per visualizzare gli stati alternativi (errore, successo, token scaduto).

---

> **PROSSIMO PASSO:** esegui `/eq-implement US-001` per implementare. L'Engineer leggerà `style-tokens.json` come contratto visivo e questi mockup come riferimento layout.
