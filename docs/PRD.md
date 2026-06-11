# PRD — GOLP

**Data:** 2026-06-11 | **Stato:** v3

## Visione

App mobile multi-tenant per circoli sportivi amatoriali di sport a squadre simmetriche — formato 2v2 nel MVP (Padel, Beach Tennis, Basket 2v2, Burraco...), con estensione futura a 1v1 e più in generale NvN — che sostituisce il ranking soggettivo con una classifica oggettiva calcolata in tempo reale sui risultati reali delle partite, convalidati dai giocatori stessi.

## Personas

### Marco — Giocatore amatoriale
- **Contesto:** Gioca 2-3 volte a settimana in un circolo, partite casual tra soci.
- **Pain:** Non sa quanto vale oggettivamente rispetto agli altri. Il ranking attuale è percezione, non dato — genera discussioni e insoddisfazione.
- **Job-to-be-done:** Voglio vedere la mia posizione reale in classifica, sapere con chi gioco meglio e contro chi faccio più fatica, basandomi su partite effettivamente giocate.

### Sara — Organizzatrice circolo (secondaria, post-MVP)
- **Contesto:** Gestisce comunicazioni e iscrizioni del circolo, riceve lamentele sul ranking improvvisato.
- **Pain:** Nessuno strumento per gestire classifiche in modo neutro e automatico.
- **Job-to-be-done:** Voglio uno strumento che gestisca la classifica da solo, senza che io debba arbitrare.

## MVP Scope

### In scope
- Registrazione account giocatore + iscrizione a uno o più circoli
- Inserimento risultato partita (4 giocatori + punteggio)
- Conferma risultato da parte di tutti e 4 i giocatori prima che valga
- Classifica circolo in tempo reale dopo ogni partita confermata
- Algoritmo ranking custom: vittoria/sconfitta, rank compagno, rank avversari, differenza punti
- Giocatore del mese e dell'anno
- Statistiche personali: miglior compagno, avversario più ostico

### Out of scope (per ora)
- Formati diversi dal 2v2 (1v1, NvN) — il MVP fissa `team_size = 2`; il formato per sport diventa configurabile in seguito, l'algoritmo è già compatibile (vedi sotto)
- Tornei organizzati — complessità logistica non necessaria al lancio
- Chat tra giocatori — non core al problema
- Pagamenti e abbonamenti — modello di monetizzazione da definire dopo trazione
- Dashboard admin circolo — i gestori vengono dopo i giocatori
- Statistiche avanzate — prima raccogliamo dati, poi le analizziamo

## Requisiti funzionali (high level)

1. **RF-1:** Multi-tenant — ogni circolo è uno spazio isolato; un giocatore può appartenere a più circoli
2. **RF-2:** Inserimento partita — selezione dei 4 giocatori (2 coppie) e inserimento punteggio set per set
3. **RF-3:** Validazione collettiva — la partita conta solo dopo conferma di tutti e 4 i partecipanti
4. **RF-4:** Calcolo ranking real-time — aggiornamento classifica immediato dopo conferma
5. **RF-5:** Premi automatici — giocatore del mese (reset mensile) e dell'anno (reset annuale)
6. **RF-6:** Statistiche personali — miglior partner per win-rate, avversario con win-rate più basso

## Algoritmo di ranking

ELO adattato per squadre, sport-agnostic. Formulato per N giocatori per squadra (MVP: N = 2).

```
team_rating    = media(R_giocatore1, ..., R_giocatoreN)
E_win          = 1 / (1 + 10^((R_avversari - R_squadra) / 400))
score_ratio    = punti_vincitori / (punti_vincitori + punti_perdenti)   // sempre [0.5, 1.0]
effective_result = 0.5 + (score_ratio - 0.5) × amplifier
ΔR             = K × (effective_result - E_win)
```

Parametri:
- `amplifier` = 0.7 (quanto pesa il margine rispetto al puro win/loss)
- `K` = 32 default, 48 per i primi 15 match (stabilizzazione cold start)
- Rating iniziale = 1000

**Sport config** (per-circolo, estendibile):
```json
{ "sport": "padel",     "point_unit": "games",  "sets": true,  "team_size": 2 }
{ "sport": "basket2v2", "point_unit": "points", "sets": false, "team_size": 2 }
{ "sport": "burraco",   "point_unit": "score",  "sets": false, "team_size": 2 }
```

`team_size` è fisso a 2 nel MVP ma presente nel modello dati fin da subito: in futuro abiliterà 1v1 (es. tennis singolo, scacchi) e NvN (es. calcetto 5v5) senza modifiche all'algoritmo — cambia solo il numero di giocatori selezionati e di conferme richieste (2N/2N).

`score_ratio` si calcola sommando tutte le unità di entrambe le squadre — la formula è identica per qualunque sport.

**UX:** i giocatori vedono solo `+12 pt` o `-8 pt` dopo ogni partita. L'algoritmo non viene esposto. Deve sembrare giusto e coerente, non necessariamente comprensibile.

## Architettura (decisa)

- **Frontend:** Angular PWA mobile-first — niente app store, pubblicazione via URL, deploy istantaneo
- **Backend:** ASP.NET Core minimal API + EF Core, autenticazione JWT
- **Database:** Azure SQL (relazionale) — multi-tenancy via `circolo_id` su ogni entità
- **Hosting:** Azure Static Web Apps (free tier) per la PWA; API su VM Windows Azure esistente — costi ricorrenti aggiuntivi: zero
- **Notifiche push:** Web Push (VAPID), gratuito. Limite noto: su iOS le push richiedono PWA installata in home screen (iOS 16.4+); fallback = lista partite in attesa visibile in-app

## Metriche di successo

- ≥ 2 circoli attivi entro 3 mesi dal lancio
- ≥ 20 giocatori attivi per circolo (≥ 40 totale)
- ≥ 70% partite con tutti e 4 i giocatori che confermano il risultato entro 24h

## Open questions

- Timeout conferma: cosa succede se uno dei 4 non conferma entro X ore? Partita annullata, o conta comunque con 3/4 conferme?
- Creazione circolo: auto-registration libera o invite-only tramite codice?
- Nome definitivo del prodotto: "GOLP" è il working name — da confermare

## Decisioni esplicite

- **Multi-tenant da subito** (non single-club poi espandere): i giocatori si aspettano di poter cambiare circolo o appartenerne a più d'uno; costruire single-tenant creerebbe debito tecnico immediato
- **Validazione 4/4 obbligatoria**: garantisce l'oggettività del dato, che è il valore core del prodotto; senza di essa il ranking perde credibilità
- **No admin dashboard nel MVP**: i giocatori sono il primary user e la trazione iniziale; i gestori arrivano quando c'è un prodotto funzionante da mostrare
- **Algoritmo opaco per l'utente**: l'app mostra solo `+N pt` / `-N pt` senza esporre la formula. L'obiettivo è che il risultato sembri giusto e coerente, non che sia comprensibile.
- **Sport-agnostic dal design**: la formula ELO usa `score_ratio = punti_vince / totale_punti`, valida per qualunque scala di punteggio. Ogni sport ha solo una config `point_unit + sets + team_size`.
- **MVP fisso 2v2, formato NvN rimandato**: il campo `team_size` entra nel modello dati da subito (evita migrazioni dolorose), ma UI e flussi del MVP assumono 2v2. Supporto 1v1/NvN non è prioritario — può arrivare in una release successiva senza riprogettare l'algoritmo.
- **PWA invece di app nativa** (decisione 2026-06-11): pubblicazione senza app store (zero costi/attrito di review), aggiornamenti istantanei, costi di gestione nulli sfruttando risorse Azure già attive. Stack allineato alle competenze: .NET backend, Angular frontend. Se post-trazione le push iOS diventassero critiche, si valuta wrapper Capacitor senza riscrivere il frontend.

---

> **PROSSIMO PASSO:** invoca `/eq-spec --prd docs/PRD.md` per trasformare questa visione in un backlog di user story.
