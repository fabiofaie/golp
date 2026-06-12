# GOLP — Riassunto PRD (per sviluppo)

**Fonte:** docs/PRD.md v3 (2026-06-11)

## Cos'è
PWA mobile multi-tenant per circoli sportivi amatoriali. Sostituisce il ranking soggettivo con una classifica oggettiva calcolata in tempo reale sulle partite reali, convalidate dai giocatori stessi. MVP: formato **2v2** (Padel, Beach Tennis, Basket 2v2, Burraco). Estensione futura a 1v1 e NvN già prevista nel modello.

## Personas
- **Marco — Giocatore amatoriale (primary):** gioca 2-3 volte/settimana; vuole sapere il suo valore oggettivo, con chi gioca meglio e contro chi fa più fatica.
- **Sara — Organizzatrice circolo (secondaria, post-MVP):** vuole una classifica automatica e neutra, senza dover arbitrare.

## MVP — In scope
- Registrazione account giocatore + iscrizione a uno o più circoli
- Inserimento risultato partita (4 giocatori + punteggio set/punti)
- Conferma risultato da parte di **tutti e 4** i giocatori prima che valga
- Classifica circolo in tempo reale dopo ogni partita confermata
- Algoritmo ranking custom (vittoria/sconfitta, rank compagno, rank avversari, differenza punti)
- Giocatore del mese e dell'anno (reset mensile/annuale)
- Statistiche personali: miglior compagno (win-rate), avversario più ostico (win-rate più basso)

## MVP — Out of scope
Formati ≠ 2v2 · Tornei · Chat · Pagamenti/abbonamenti · Dashboard admin circolo · Statistiche avanzate

## Requisiti funzionali
- **RF-1** Multi-tenant: ogni circolo è isolato; un giocatore può stare in più circoli
- **RF-2** Inserimento partita: 4 giocatori (2 coppie) + punteggio set per set
- **RF-3** Validazione collettiva: vale solo dopo conferma di tutti e 4
- **RF-4** Ranking real-time: aggiornamento immediato dopo conferma
- **RF-5** Premi automatici: giocatore del mese e dell'anno
- **RF-6** Statistiche personali: miglior partner, avversario più ostico

## Algoritmo di ranking (ELO adattato per squadre, sport-agnostic)
Formulato per N giocatori per squadra (MVP: N=2).

```
team_rating      = media(R_giocatore1, ..., R_giocatoreN)
E_win            = 1 / (1 + 10^((R_avversari - R_squadra) / 400))
score_ratio      = punti_vincitori / (punti_vincitori + punti_perdenti)   // [0.5, 1.0]
effective_result = 0.5 + (score_ratio - 0.5) × amplifier
ΔR               = K × (effective_result - E_win)
```

**Parametri:**
- `amplifier` = 0.7 (peso del margine vs puro win/loss)
- `K` = 32 default, **48 per i primi 15 match** (stabilizzazione cold start)
- Rating iniziale = **1000** (per-circolo, non globale)

**Note:** `score_ratio` somma tutte le unità di entrambe le squadre — formula identica per ogni sport. UX: i giocatori vedono solo `+12 pt` / `-8 pt`; l'algoritmo **non viene esposto** (deve sembrare giusto e coerente, non comprensibile).

## Sport config (per-circolo, estendibile)
```json
{ "sport": "padel",     "point_unit": "games",  "sets": true,  "team_size": 2 }
{ "sport": "basket2v2", "point_unit": "points", "sets": false, "team_size": 2 }
{ "sport": "burraco",   "point_unit": "score",  "sets": false, "team_size": 2 }
```
`team_size` fisso a 2 nel MVP ma **presente nel modello da subito** → abiliterà 1v1 e NvN senza toccare l'algoritmo (cambia solo numero di giocatori selezionati e conferme richieste, 2N/2N).

## Architettura (decisa)
- **Frontend:** Angular PWA mobile-first (no app store, deploy via URL)
- **Backend:** ASP.NET Core minimal API + EF Core, auth JWT
- **DB:** Azure SQL relazionale — multi-tenancy via `circolo_id`/`circle_id` su ogni entità
- **Hosting:** Azure Static Web Apps (free) per la PWA; API su VM Windows Azure esistente — costi aggiuntivi zero
- **Push:** Web Push (VAPID). Limite iOS: push solo con PWA installata in home (iOS 16.4+); fallback = lista partite in attesa in-app

## Ciclo di vita partita
`pending` → `confirmed` (4/4 conferme) **oppure** `disputed` (un rifiuto). Solo le partite `confirmed` aggiornano il rating.

## Metriche di successo
- ≥ 2 circoli attivi entro 3 mesi dal lancio
- ≥ 20 giocatori attivi per circolo (≥ 40 totali)
- ≥ 70% partite con tutti e 4 i giocatori che confermano entro 24h

## Open questions (da decidere)
- **Timeout conferma:** partita annullata o valida con 3/4 se uno non conferma entro X ore?
- **Creazione circolo:** auto-registration libera o invite-only tramite codice? (`Circle.IsPrivate` e `JoinCode` già nel modello, non esposti)
- **Nome prodotto:** "GOLP" è working name, da confermare

## Decisioni esplicite
- Multi-tenant da subito (evita debito tecnico)
- Validazione 4/4 obbligatoria (oggettività = valore core)
- No admin dashboard nel MVP (i giocatori vengono prima dei gestori)
- Algoritmo opaco per l'utente
- Sport-agnostic dal design (config `point_unit + sets + team_size` per sport)
- MVP fisso 2v2, NvN rimandato (ma `team_size` nel modello da subito)
- PWA invece di app nativa (zero costi/attrito review; eventuale wrapper Capacitor post-trazione per push iOS)
