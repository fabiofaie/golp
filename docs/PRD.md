# GOLP — Riassunto PRD (per sviluppo)

**Fonte:** docs/PRD.md v3 (2026-06-11) | **Aggiornato:** 2026-07-01

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
- Algoritmo ranking custom (vittoria/sconfitta, rank compagno, rank avversari, differenza punti, peso set)
- Giocatore del mese e dell'anno (reset mensile/annuale) con notifica email automatica
- Statistiche personali: miglior compagno (win-rate), avversario più ostico (win-rate più basso)
- Sistema inviti tramite link con flusso differenziato nuovo/esistente utente
- Sessioni lunghe con refresh token (90 giorni sliding window)
- Profilo utente: tema chiaro/scuro, notifiche push, modifica nome, logout tutti device, eliminazione account
- Sistema email su template HTML riutilizzabili
- Ospiti nelle partite + pagina pubblica conferma via token + Quick Match
- PWA: guida installazione contestuale + aggiornamento automatico

## MVP — Out of scope
Formati ≠ 2v2 · Tornei · Chat · Pagamenti/abbonamenti · Dashboard admin circolo · Statistiche avanzate · Verifica email alla registrazione · Social login

## Requisiti funzionali
- **RF-1** Multi-tenant: ogni circolo è isolato; un giocatore può stare in più circoli
- **RF-2** Inserimento partita: 4 giocatori (2 coppie) + punteggio set per set; il proprietario può registrare senza partecipare; slot ospite (nome + contatto) per giocatori non registrati
- **RF-3** Validazione collettiva: vale solo dopo conferma di tutti e 4; il proprietario può forzare conferma su partite `pending`; pagina pubblica conferma via token temporaneo (72h, no auth)
- **RF-4** Ranking real-time: aggiornamento immediato dopo conferma; notifica push a chi sale di posizione
- **RF-5** Premi automatici: giocatore del mese e dell'anno; job schedulato invia email al vincitore
- **RF-6** Statistiche personali: miglior partner, avversario più ostico (soglia minima N=3 partite)
- **RF-7** Sport configurabili: tabella `Sports` in DB con `IsActive`; aggiunta sport senza deploy
- **RF-8** Sessioni lunghe: refresh token sliding window 90 giorni; revoca singola o tutti i device; revoca automatica su cambio password
- **RF-9** Growth loop: link invito circolo (token stabile), join flow differenziato, ospiti con `IsActivated=false`, Quick Match (crea circolo+partita atomicamente), condivisione via WhatsApp

## Algoritmo di ranking (ELO adattato per squadre, sport-agnostic)
Formulato per N giocatori per squadra (MVP: N=2).

```
team_rating      = media(R_giocatore1, ..., R_giocatoreN)
E_win            = 1 / (1 + 10^((R_avversari - R_squadra) / 400))
score_ratio      = punti_vincitori / (punti_vincitori + punti_perdenti)   // [0.5, 1.0]
effective_result = 0.5 + (score_ratio - 0.5) × amplifier
ΔR               = K × (effective_result - E_win)
```

**Per sport a set** (`sets: true`):
```
score_ratio = α × set_ratio + (1-α) × game_ratio
set_ratio   = sets_vinti / (sets_vinti + sets_persi)
game_ratio  = games_vinti_totali / (games_vinti_totali + games_persi_totali)
```
- `α` (`set_weight`) = 0.4 per padel/beach tennis; 0.0 (ignorato) per sport senza set
- Pareggio parziale: set pari → usa solo game_ratio; game pari → usa solo point_unit ratio
- Delta minimo ±1 quando esiste un vincitore reale (evita arrotondamento a 0)

**Parametri:**
- `amplifier` = 0.7 (peso del margine vs puro win/loss)
- `K` = 32 default, **48 per i primi 15 match** (stabilizzazione cold start)
- Rating iniziale = **1000** (per-circolo, non globale)

**Note:** `score_ratio` somma tutte le unità di entrambe le squadre — formula identica per ogni sport. UX: i giocatori vedono solo `+12 pt` / `-8 pt`; l'algoritmo **non viene esposto**.

## Sport config (per-circolo, da database)
```json
{ "sport": "padel",       "point_unit": "games",  "sets": true,  "set_weight": 0.4, "team_size": 2 }
{ "sport": "beachtennis", "point_unit": "games",  "sets": true,  "set_weight": 0.4, "team_size": 2 }
{ "sport": "basket2v2",   "point_unit": "points", "sets": false, "set_weight": 0.0, "team_size": 2 }
{ "sport": "burraco",     "point_unit": "score",  "sets": false, "set_weight": 0.0, "team_size": 2 }
```
Sport gestiti in tabella DB (`Sports`, colonne: `Key`, `DisplayName`, `PointUnit`, `Sets`, `SetWeight`, `TeamSize`, `IsActive`). Aggiunta nuovi sport senza deploy: solo INSERT sul DB.

`team_size` fisso a 2 nel MVP ma **presente nel modello da subito** → abiliterà 1v1 e NvN senza toccare l'algoritmo.

## Architettura (decisa)
- **Frontend:** Angular PWA mobile-first (no app store, deploy via URL)
- **Backend:** ASP.NET Core minimal API + EF Core, auth JWT + refresh token
- **DB:** Azure SQL relazionale — multi-tenancy via `circle_id` su ogni entità
- **Hosting:** Azure Static Web Apps (free) per la PWA; API su VM Windows Azure esistente — costi aggiuntivi zero
- **Push:** Web Push (VAPID) tramite Firebase Cloud Messaging. Limite iOS: push solo con PWA installata (iOS 16.4+); fallback = lista partite pending in-app
- **Email:** SMTP con template HTML in `EmailTemplates/`; `DevelopmentEmailService` (console-only) in dev

## Ciclo di vita partita
`pending` → `confirmed` (4/4 conferme **oppure** forzatura proprietario) | `disputed` (un rifiuto). Solo `confirmed` aggiorna il rating.

Conferma può avvenire:
1. Autenticata: dal flusso in-app (`/circles/:id/matches/:id`)
2. Pubblica: via token temporaneo (`/m/{token}`, 72h, no login) — anche per ospiti non registrati

## Gestione circolo — privilegi proprietario
- Generare link di invito (token stabile, non scade)
- Aggiungere manualmente un giocatore (crea account ghost se email non esiste)
- Registrare partite senza partecipare (no conferma implicita 1/4)
- Forzare conferma su partite `pending` (audit trail: `ForceConfirmedById/At`); richiede conferma UI esplicita

## Growth loop
- **Ospiti:** ogni slot partita accetta "Membro" o "Ospite" (nome + email/telefono). Se email non esiste → `User` con `IsActivated=false` + `CircleMembership` rating 1000. Ospite può attivare account via "Recupera password". `Contact Picker API` su mobile per precompilare da rubrica.
- **Pagina pubblica conferma:** `GET /m/{token}` no-auth, mostra partita e permette conferma/disputa. CTA "Crea account" per ospiti non attivati.
- **Quick Match:** dalla dashboard, crea circolo privato + partita in un'unica transazione (3 giocatori: membro o ospite). Rilevamento circolo duplicato exact-4/4.
- **WhatsApp:** post-registrazione partita, pulsante "Invia su WhatsApp" per ogni partecipante con telefono → `wa.me/<phone>?text=...` con link conferma token.

## Profilo utente
Pagina `/profilo` protetta (da US-028). Funzioni:
- **Tema:** scuro (default) / chiaro. Persistenza `localStorage`, non account.
- **Notifiche push:** toggle on/off + pulsante test. Se non PWA installata → guida installazione.
- **Nome visualizzato:** modificabile, aggiornato real-time senza re-login.
- **Riepilogo circoli:** lista circoli con rating attuale per ognuno.
- **Logout tutti device:** revoca tutti i refresh token + security stamp.
- **Elimina account:** soft-delete (anonimizzazione: nome → "Utente eliminato", email/password invalidate). Partite confirmed storiche intatte. Partite pending annullate.

## Sistema email
Template HTML in `EmailTemplates/` con layout condiviso (header/footer GOLP). Motore: `IEmailTemplateRenderer` (placeholder `{{Var}}`). `SmtpEmailService` in prod, `DevelopmentEmailService` (console) in dev.

Email inviate:
| Evento | Destinatario |
|--------|-------------|
| Registrazione nuova | Staff `iscrizioni.golp@eqproject.it` |
| Nuovo circolo creato | Staff `iscrizioni.golp@eqproject.it` |
| Partita creata (richiesta conferma) | 3 giocatori non-inseritore |
| Partita contestata | Proprietario circolo |
| Reset password | Utente |
| Aggiunta manuale al circolo | Utente aggiunto |
| Attivazione account ghost | Utente ghost |
| Giocatore del mese/anno | Vincitore (job schedulato, primo giorno mese/anno) |

Tutte fire-and-forget: fallimento email non blocca l'operazione principale.

## PWA
- **Guida installazione:** banner non bloccante al primo accesso da browser (non da standalone). Mini-guida contestuale per browser+OS: Safari iOS ("Condividi → Aggiungi"), Chrome Android (prompt nativo `beforeinstallprompt`). Mostrata una sola volta.
- **Aggiornamento automatico:** check su ritorno in foreground e navigazione tra pagine. Banner "Nuova versione disponibile — Aggiorna" (no reload automatico forzato). `index.html` no-cache; bundle con hash: cache lunga.
- **Versione build:** visibile in login e dashboard. Generata da script (deterministico da commit hash).

## Metriche di successo
- ≥ 2 circoli attivi entro 3 mesi dal lancio
- ≥ 20 giocatori attivi per circolo (≥ 40 totali)
- ≥ 70% partite con tutti e 4 i giocatori che confermano entro 24h

## Open questions (da decidere)
- **Timeout conferma:** partita annullata o valida con 3/4 se uno non conferma entro X ore?
- **Nome prodotto:** "GOLP" è working name, da confermare

## Decisioni esplicite
- Multi-tenant da subito (evita debito tecnico)
- Validazione 4/4 obbligatoria (oggettività = valore core); forzatura proprietario come escape hatch
- No admin dashboard nel MVP (i giocatori vengono prima dei gestori)
- Algoritmo opaco per l'utente (simulatore disponibile ma formula non esposta)
- Sport-agnostic dal design (config `point_unit + sets + set_weight + team_size` per sport, da DB)
- MVP fisso 2v2, NvN rimandato (ma `team_size` nel modello da subito)
- PWA invece di app nativa (zero costi/attrito review)
- Creazione circolo libera (non invite-only); `IsPrivate` nel modello per Quick Match e futuro
- Soft-delete account (anonimizzazione, non hard delete — FK storiche intatte)
- Refresh token 90 giorni sliding window (no scadenza assoluta fissa)
- Email fire-and-forget (fallimento SMTP non blocca flusso utente)
