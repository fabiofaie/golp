# GOLP — Product Requirements Document

**Versione:** 4.0 (definitiva) · **Data:** 2026-07-06
**Fonte:** ricostruito da codice (`src/`, `frontend/`) + 50 storie del backlog (`docs/BACKLOG.md`)
**Stato prodotto:** 42/50 storie DONE, 4 in REVIEW, 4 TODO — MVP giocabile end-to-end

---

## 1. In una frase

> **GOLP trasforma le partite tra amici in una classifica vera.**
> Registri chi ha giocato e chi ha vinto, tutti confermano con un tap, e un rating stile ELO dice — senza discussioni — chi è davvero il più forte del gruppo.

## 2. L'elevator pitch (30 secondi)

Nei gruppi sportivi amatoriali "chi è il più forte" è sempre una discussione da bar: opinioni, memoria selettiva, nessun dato. GOLP la chiude.

Ogni partita 2v2 (o 1v1) viene registrata con il punteggio reale. Perché conti, **tutti e 4 i giocatori devono confermarla** — niente risultati gonfiati. Alla conferma, un algoritmo ELO adattato aggiorna il rating di ognuno e la classifica del circolo si muove **in tempo reale**. Il giocatore vede solo `+12` o `−8`: la matematica resta sotto il cofano.

È una **PWA** — si installa dal browser, zero app store, zero costi di distribuzione. Ogni gruppo (il "circolo") è uno spazio isolato con la sua classifica. Puoi giocare in più circoli, ognuno con un rating indipendente.

E cresce da sola: inviti via link, aggiungi amici come **ospiti** anche se non hanno l'account, mandi il link di conferma su **WhatsApp**. Ogni partita registrata è un potenziale nuovo utente.

## 3. Il problema → la soluzione

| Il problema (oggi) | La soluzione GOLP |
|---|---|
| "Chi è il più forte?" è opinione, non dato | Rating ELO oggettivo, calcolato sulle partite reali |
| Risultati raccontati a memoria, gonfiati | Validazione **4/4**: vale solo se confermano tutti |
| Serve un arbitro/organizzatore che tenga i conti | Classifica **automatica** e neutra, aggiornata da sola |
| App da scaricare, attrito, costi store | **PWA** installabile da link, gratis |
| "Con chi gioco meglio? Chi mi mette in crisi?" | Statistiche personali: miglior compagno, avversario più ostico |
| Gli amici non registrati restano fuori | **Ospiti** in partita + conferma pubblica via link, zero account richiesto |

## 4. Per chi è

- **Marco — Giocatore amatoriale (primario).** Gioca 2-3 volte a settimana a padel/beach/basket. Vuole sapere quanto vale davvero, con chi rende meglio e contro chi soffre. È lui il cuore del prodotto: **i giocatori vengono prima dei gestori**.
- **Sara — Organizzatrice di circolo (secondaria, post-MVP).** Vuole una classifica automatica e neutra senza dover fare da arbitro. Nel MVP non ha una dashboard admin dedicata; usa gli stessi strumenti + i privilegi da proprietario del circolo.

## 5. Cosa fa GOLP — le capacità

### 5.1 Account e circoli (multi-tenant)
- Registrazione con nome, email, password (JWT + BCrypt). Recupero password via email (token 1h).
- **Sessioni lunghe**: access token breve + refresh token **90 giorni sliding window**. Usi l'app anche una volta ogni 3 mesi e resti loggato. Logout singolo o da tutti i device; cambio password revoca tutto.
- Un **circolo** è uno spazio isolato per uno sport. Ogni entità porta `circle_id`: **i dati di un circolo non escono mai da lì**.
- Un giocatore sta in **più circoli** contemporaneamente, con un **rating indipendente per circolo** (parte da 1000).

### 5.2 Partite e validazione (il cuore del dato)
- Registri una partita **2v2** (o **1v1** dove lo sport lo consente): 4 (o 2) giocatori, punteggio set-per-set o punteggio singolo secondo lo sport.
- Ciclo di vita: **`pending` → `confirmed` → (o `disputed`)**. Solo `confirmed` tocca il rating.
- **Conferma 4/4**: l'inseritore conferma implicitamente (1/4); gli altri confermano uno a uno. Un rifiuto → `disputed`, fuori dalla classifica.
- **Conferma pubblica via link**: chi riceve `golp.app/m/{token}` conferma o contesta **senza login** (token valido 72h). Funziona anche per gli ospiti non registrati.
- **Privilegi del proprietario**: registrare partite a cui non partecipa; **forzare la conferma** di partite bloccate (con avviso esplicito di irreversibilità + audit trail).
- **Pagina di dettaglio partita**: risultato, date, chi ha chiuso la conferma, e il delta rating di ognuno.

### 5.3 Ranking e classifica (il valore core)
- Alla conferma, il **rating ELO** dei giocatori si aggiorna nella stessa transazione. La **classifica del circolo** si muove subito.
- Il giocatore vede solo `+N pt` / `−N pt`. **L'algoritmo non è mai esposto** (esiste però una pagina "Come funziona" + un simulatore per chi è curioso).
- Podio top-3, evidenziazione della propria posizione, sezione "non classificati" (0 partite).
- **Notifica push** quando **sali** di posizione in classifica.

### 5.4 Premi e statistiche (gamification leggera)
- **Giocatore del mese** e **dell'anno** per circolo, reset a mese/anno, storico consultabile.
- Email automatica al vincitore (job schedulato in-process, idempotente).
- **Statistiche personali**: miglior compagno (win-rate più alto in coppia) e avversario più ostico (win-rate più basso contro), soglia minima 3 partite.

### 5.5 Growth loop (crescita virale)
- **Link di invito** al circolo (token stabile, non scade) → copia o email.
- **Flusso invito differenziato**: "hai già usato GOLP?" → nuovo va a registrazione, esistente va a login con auto-join.
- **Ospiti**: aggiungi chiunque a uno slot partita (nome + email/telefono), anche senza account. Il sistema crea un `User` `IsActivated=false` che può attivarsi con "Recupera password". **Contact Picker API** per pescare dalla rubrica su mobile.
- **Quick Match**: dalla dashboard, registri una partita **creando il circolo al volo** in un'unica transazione. Rileva circoli duplicati (stessi 4 membri + sport) e ti fa scegliere invece di duplicare.
- **Condivisione WhatsApp / Web Share**: dopo la partita, un tap manda il link di conferma via WhatsApp (numero precompilato) o al selettore nativo del telefono.
- **Aggiunta manuale** di un giocatore da parte del proprietario (crea account ghost se l'email non esiste).

### 5.6 Profilo utente
Pagina `/profilo` (area autenticata):
- **Tema** chiaro/scuro (default scuro), persistito in `localStorage` per device, contrasto WCAG AA verificato.
- **Notifiche push** on/off + invio di test + guida se l'app non è installata.
- **Nome visualizzato** modificabile, aggiornato ovunque senza re-login.
- **Riepilogo circoli** con rating attuale per ciascuno.
- **Logout da tutti i device** (security stamp + revoca refresh token).
- **Elimina account**: soft-delete/anonimizzazione (nome → "Utente eliminato", credenziali invalidate). Le partite `confirmed` storiche restano coerenti per gli altri; le `pending` vengono annullate.

### 5.7 PWA e operatività
- **Guida installazione** contestuale al primo accesso da browser: mini-guida per browser+OS (Safari iOS "Condividi → Aggiungi", Chrome Android prompt nativo). Mostrata una volta sola, non invasiva.
- **Aggiornamento automatico**: banner "Nuova versione disponibile" al ritorno in foreground/navigazione (no reload forzato). `index.html` no-cache, bundle hashati con cache lunga.
- **Numero di versione** deterministico dal commit, visibile in login e dashboard, per sapere quale build gira su prod/test.
- **Branding per ambiente**: logo/icona arancione (prod), giallo (test), verde (dev).

### 5.8 Sistema email
Template HTML riutilizzabili in `EmailTemplates/` con layout condiviso e placeholder `{{Var}}`. `SmtpEmailService` in prod, `DevelopmentEmailService` (console) in dev. **Tutte fire-and-forget**: un fallimento SMTP non blocca mai l'operazione utente.

| Evento | Destinatario |
|---|---|
| Nuova registrazione / nuovo circolo | Staff `iscrizioni.golp@eqproject.it` |
| Partita creata (richiesta conferma) | I giocatori da confermare |
| Partita contestata | Proprietario del circolo |
| Reset password / attivazione ghost / aggiunta al circolo | Utente interessato |
| Giocatore del mese/anno | Vincitore |

---

## 6. L'algoritmo di ranking (ELO adattato per squadre, sport-agnostic)

Formulato per N giocatori a squadra (MVP: N=1 o 2). Il giocatore **non vede mai la formula**.

```
team_rating      = media(rating dei giocatori della squadra)   // singolo: rating del solo giocatore
E_win            = 1 / (1 + 10^((R_avversari − R_squadra) / 400))
score_ratio      = punti_vincitori / (punti_vincitori + punti_perdenti)   // clamp [0.5, 1.0]
effective_result = 0.5 + (score_ratio − 0.5) × amplifier
ΔR               = K × (effective_result − E_win)
```

**Sport a set** (`Sets = true`, es. padel, beach tennis):
```
score_ratio = α × set_ratio + (1−α) × game_ratio
set_ratio   = set_vinti  / (set_vinti + set_persi)
game_ratio  = game_vinti / (game_vinti + game_persi)   // somma su tutti i set
```

**Parametri:**
- `amplifier` = **0.7** (peso del margine vs puro win/loss)
- `K` = **32**, **48** per i primi 15 match (stabilizzazione cold-start)
- `α` (`SetWeight`) = **0.4** per sport a set, 0 altrimenti
- Rating iniziale = **1000** per-circolo

**Pareggi parziali** (US-034): set pari → conta solo `game_ratio`; game pari → conta solo il `point_unit`. Delta minimo **±1** quando esiste un vincitore reale (mai 0). Il pareggio totale resta bloccato in creazione.

---

## 7. Configurazione sport (da database)

Tabella `Sports` — aggiungere/modificare uno sport è una **INSERT, senza deploy**.

| Key | PointUnit | Sets | SetWeight | TeamSize | AllowsSingles |
|---|---|---|---|---|---|
| `padel` | games | ✔ | 0.4 | 2 | ✔ |
| `beachtennis` | games | ✔ | 0.4 | 2 | ✔ |
| `basket2v2` | points | ✗ | 0.0 | 2 | ✗ |
| `burraco` | score | ✗ | 0.0 | 2 | ✗ |

`TeamSize` è nel modello da subito: abiliterà NvN senza toccare l'algoritmo.

---

## 8. Architettura

- **Frontend:** Angular 19 PWA mobile-first, standalone components, lazy routes, deploy via URL (Azure Static Web Apps).
- **Backend:** ASP.NET Core Minimal API (.NET 10) + EF Core, auth JWT + refresh token. Un endpoint-class per dominio.
- **DB:** Azure SQL relazionale. **Multi-tenancy via `circle_id` su ogni entità.**
- **Push:** Web Push (VAPID) via Firebase Cloud Messaging. Limite iOS: push solo con PWA installata (iOS 16.4+); fallback = lista pending in-app.
- **Email:** SMTP + template HTML; console-only in dev.
- **Job schedulati:** `BackgroundService` in-process (giocatore del mese/anno), nessun processo Azure aggiuntivo → **costi incrementali zero**.

---

## 9. Confini del MVP

**In scope:** account + circoli · partite 2v2/1v1 + validazione 4/4 · ranking ELO real-time · premi mese/anno · statistiche personali · inviti + ospiti + Quick Match + WhatsApp · conferma pubblica via token · profilo completo · PWA (install + auto-update) · email su template.

**Out of scope (MVP):** formati NvN oltre 1v1/2v2 · tornei e campionato stagionale (EP-007, TODO) · chat · pagamenti/abbonamenti · dashboard admin di circolo · statistiche avanzate (trend, grafici, head-to-head) · verifica email alla registrazione · social login · sincronizzazione tema/preferenze cross-device.

---

## 10. Decisioni esplicite (e perché)

- **Multi-tenant da subito** — evita debito tecnico di retrofit.
- **Validazione 4/4 obbligatoria** — l'oggettività è il valore core; la forzatura del proprietario è l'unica valvola di sfogo.
- **Algoritmo opaco** — il giocatore vede l'impatto (`+12`), non la matematica; il simulatore soddisfa i curiosi senza esporre la formula come contratto.
- **Sport-agnostic dal design** — `point_unit + sets + set_weight + team_size` da DB; nuovo sport = INSERT.
- **PWA invece di nativa** — zero costi/attrito store.
- **Creazione circolo libera** (non invite-only); `IsPrivate` riservato a Quick Match e futuro.
- **Soft-delete account** — anonimizzazione, FK storiche intatte.
- **Refresh token sliding 90gg** — nessuna scadenza assoluta fissa.
- **Email fire-and-forget** — SMTP giù non deve mai bloccare l'utente.
- **Growth by design** — ogni partita è un canale di acquisizione (ospiti, link, WhatsApp).

---

## 11. Metriche di successo

- ≥ 2 circoli attivi entro 3 mesi dal lancio.
- ≥ 20 giocatori attivi per circolo (≥ 40 totali).
- ≥ 70% delle partite confermate da tutti e 4 entro 24h.

---

## 12. Roadmap oltre l'MVP

- **EP-007 — Campionato del circolo (TODO):** "serata al circolo" con proposta accoppiamenti dai presenti e classifica a **punti solo positivi** (chi gioca sale, chi non gioca non viene scavalcato da fermo); torneo one-shot formato Americano/Mexicano. Risolve la demotivazione del giocatore attivo scavalcato da chi non gioca — trasforma GOLP da registro a **organizzatore di serate**.
- **Storie TODO minori:** ri-invio link conferma da ogni punto (US-043), pulsante "torna all'ultimo circolo" (US-045).
- **Futuro:** formati NvN, dashboard organizzatore, statistiche avanzate, data export GDPR.

---

## 13. Questioni aperte

- **Timeout conferma:** partita annullata o valida 3/4 se uno non conferma entro X ore? (oggi risolto solo via forzatura proprietario)
- **Nome prodotto:** "GOLP" è ancora working name.
- **Campionato (EP-007):** modello check-in, valori punti, durata stagione — da validare coi test user.
