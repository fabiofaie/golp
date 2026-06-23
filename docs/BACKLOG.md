# Backlog — GOLP

**Generato il:** 2026-06-11 | **Ultima modifica:** 2026-06-23

## Riepilogo

- Epic totali: 5
- Storie totali: 25
- Storie TODO: 2 | PLANNED: 1 | IN_PROGRESS: 0 | REVIEW: 5 | DONE: 17

---

## EP-001 — Onboarding e Circoli

_Fondamenta multi-tenant: account giocatore, creazione circolo, appartenenza a più circoli. Senza questo nessun'altra storia è giocabile. (da PRD §RF-1)_

#### US-001: Registrazione e accesso account giocatore

**Epic:** EP-001 | **Priority:** HIGH | **Story Points:** 3 | **Status:** DONE
**Approved (2026-06-11):** Review umana OK.
**Review note (2026-06-11):** Codice in `src/Golp.Api/` (Minimal API, EF Core, JWT, BCrypt), test in `src/Golp.Tests/` (5 unit + 14 integration). Frontend in `frontend/golp-app/src/app/auth/` (4 componenti standalone, AuthService, guard, interceptor). E2E spec in `frontend/golp-app/e2e/`. Reviewer APPROVE — no critical aperti.
**Visual evidence (2026-06-11):** docs/test-results/US-001/report.md (7 AC pass / 0 AC fail / 0 console errors inattesi)
**Blocked by:** -

**Story**
Come Marco (giocatore amatoriale), voglio registrarmi con email e password e accedere all'app, così che la mia identità e le mie partite siano riconducibili a me.

**Demonstrates**
Un utente nuovo si registra, riceve un token di sessione (JWT) e rivede i propri dati al login successivo.

**Acceptance Criteria**

- [ ] Registrazione con nome, email e password crea un account e autentica subito l'utente
- [ ] Login con credenziali valide restituisce un token JWT; con credenziali errate restituisce errore esplicito senza rivelare quale campo è sbagliato
- [ ] Email già registrata → errore chiaro, nessun account duplicato
- [ ] Password sotto i requisiti minimi (lunghezza ≥ 8) rifiutata in fase di registrazione
- [ ] Il token scaduto o assente blocca l'accesso alle API protette
- [ ] Recupero password via email: l'utente inserisce l'email, riceve un link con token temporaneo (scadenza 1 ora), imposta una nuova password; il vecchio token di sessione viene invalidato
- [ ] Link di recupero già usato o scaduto → errore chiaro, nessuna modifica

**Out of scope**

- Social login (Google/Apple)
- Profilo avanzato (foto, bio)

**Open questions**

- Serve verifica email alla registrazione, o si accetta l'account subito?

---

#### US-002: Creazione circolo con configurazione sport

**Epic:** EP-001 | **Priority:** HIGH | **Story Points:** 3 | **Status:** DONE
**Approved (2026-06-11):** Review umana OK.
**Blocked by:** US-001
**Review note (2026-06-11):** Backend in `src/Golp.Api/Endpoints/CircleEndpoints.cs`, `Services/SportsConfig.cs`, entità in `Data/Entities/`. Frontend in `frontend/golp-app/src/app/circles/`. Test in `src/Golp.Tests/Integration/CircleIntegrationTests.cs` (12 test, 31 totali verdi). Reviewer APPROVE. `IsPrivate`+`JoinCode` nel modello ma non nel DTO — intenzionale per future US. **PROSSIMO PASSO:** revisione umana. Quando approvi: `/eq-approve US-002`.

**Story**
Come giocatore autenticato, voglio creare un circolo scegliendo lo sport praticato, così che il mio gruppo abbia uno spazio isolato con regole di punteggio corrette.

**Demonstrates**
Un utente crea il circolo "Padel Club Roma" con sport `padel`; il circolo appare nella sua lista e la config sport (`point_unit`, `sets`, `team_size`) è persistita.

**Acceptance Criteria**

- [ ] Creazione circolo richiede nome e sport scelto da lista predefinita (padel, beach tennis, basket 2v2, burraco)
- [ ] La config sport (`point_unit`, `sets`, `team_size=2`) viene assegnata automaticamente dallo sport scelto (da PRD §Sport config)
- [ ] Il creatore risulta automaticamente membro del circolo
- [ ] Nome circolo vuoto o duplicato per lo stesso creatore → errore di validazione
- [ ] Ogni circolo è uno spazio isolato: dati di un circolo mai visibili da un altro (da PRD §RF-1)

**Out of scope**

- Formati diversi dal 2v2: `team_size` è fisso a 2 nel MVP (da PRD §Out of scope)
- Modifica sport dopo la creazione
- Ruoli admin/gestore (Sara è post-MVP)

**Open questions**

- Creazione libera o invite-only tramite codice? (open question già nel PRD — da decidere prima del plan)

---

#### US-003: Iscrizione a uno o più circoli

**Epic:** EP-001 | **Priority:** HIGH | **Story Points:** 3 | **Status:** DONE
**Approved (2026-06-11):** Review umana OK.
**Blocked by:** US-002
**Review note (2026-06-11):** Backend in `src/Golp.Api/Endpoints/CircleEndpoints.cs` (+3 endpoint: GET /circles, POST /circles/{id}/join, GET /circles/{id}/members). Test in `src/Golp.Tests/Integration/CircleIntegrationTests.cs` (nuove classi `JoinCircleTests` + `MembersAndDiscoveryTests`, 42 test totali verdi). Frontend in `frontend/golp-app/src/app/circles/browse-circles/` (BrowseCirclesComponent), `circle.service.ts` (+3 metodi +3 interface), `app.routes.ts` (+route /circles/browse), `my-circles.component.html` (link Scopri circoli), `styles.scss` (`.member-badge`, `.btn-join`). Reviewer APPROVE. **PROSSIMO PASSO:** revisione umana. Quando approvi: `/eq-approve US-003`.
**Visual evidence (2026-06-11):** docs/test-results/US-003/report.md (5 AC pass / 0 AC fail / 0 console errors)

**Story**
Come Marco, voglio iscrivermi a uno o più circoli esistenti, così che possa giocare e comparire in classifica in ognuno di essi.

**Demonstrates**
Un utente si unisce a due circoli diversi e li vede entrambi nella propria lista; in ciascuno parte con rating iniziale 1000 indipendente.

**Acceptance Criteria**

- [ ] Un giocatore può unirsi a un circolo esistente e appare nella lista membri
- [ ] Lo stesso giocatore può appartenere a più circoli contemporaneamente (da PRD §RF-1)
- [ ] Il rating è per-circolo: iscrizione a un nuovo circolo parte da 1000, senza influenzare gli altri
- [ ] Doppia iscrizione allo stesso circolo → errore, nessun duplicato
- [ ] La lista membri di un circolo mostra solo i suoi iscritti

**Out of scope**

- Abbandono/espulsione dal circolo
- Approvazione manuale delle iscrizioni da parte di un gestore

**Open questions**

- Il meccanismo di join dipende dalla risposta su invite-only vs libero (vedi US-002)

---

## EP-002 — Partite e Validazione

_Il cuore del dato oggettivo: inserire una partita 2v2 e farla confermare da tutti e 4 i giocatori. Senza conferma 4/4 il ranking perde credibilità. (da PRD §RF-2, §RF-3)_

#### US-004: Inserimento risultato partita 2v2

**Epic:** EP-002 | **Priority:** HIGH | **Story Points:** 5 | **Status:** DONE
**Approved (2026-06-11):** Review umana OK.
**Blocked by:** US-003
**Review note (2026-06-11):** Backend in `src/Golp.Api/Endpoints/MatchEndpoints.cs`, entities in `src/Golp.Api/Data/Entities/Match.cs|MatchSet.cs`, migration `AddMatchesAndMatchSets`. Tests in `src/Golp.Tests/Integration/MatchIntegrationTests.cs` (13 tests, 55 totali verdi). Frontend in `frontend/golp-app/src/app/circles/record-match/` (RecordMatchComponent), `match.service.ts`, route `circles/:circleId/match/new`, link "Registra partita" su ogni circle card. `GET /circles/me` ora include `sets` + `pointUnit`. Reviewer APPROVE — no critical aperti. > **PROSSIMO PASSO:** revisione umana. Quando approvi, lancia `/eq-approve US-004`.

**Story**
Come Marco, voglio registrare una partita selezionando i 4 giocatori (2 coppie) e inserendo il punteggio, così che il risultato entri nel sistema e possa essere convalidato.

**Demonstrates**
Un utente inserisce una partita di padel con 4 membri del circolo e punteggio set per set; la partita appare in stato "in attesa di conferma".

**Acceptance Criteria**

- [ ] Selezione di 4 giocatori distinti, tutti membri del circolo, divisi in 2 coppie (da PRD §RF-2)
- [ ] Inserimento punteggio coerente con la config sport: set per set se `sets=true`, punteggio singolo altrimenti
- [ ] L'inseritore deve essere uno dei 4 giocatori della partita
- [ ] Giocatore duplicato nelle coppie o non membro del circolo → errore di validazione
- [ ] Punteggio senza vincitore (pareggio o set incompleti) → errore: la partita deve avere una squadra vincente
- [ ] La partita creata è in stato `pending` e non tocca la classifica

**Out of scope**

- Modifica/cancellazione partita dopo l'inserimento
- Formati 1v1 / NvN (post-MVP)
- Partite tra circoli diversi

**Open questions**

- Per gli sport a set: il punteggio del singolo set va validato secondo le regole dello sport (es. 6-4 padel) o accettato libero?

---

#### US-005: Conferma collettiva del risultato (4/4)

**Epic:** EP-002 | **Priority:** HIGH | **Story Points:** 5 | **Status:** DONE
**Approved (2026-06-12):** Review umana OK.
**Review note (2026-06-12 v2):** Aggiunto `GET /circles/{circleId}/matches/{matchId}` (include sets). Aggiunto `MatchConfirmComponent` su rotta `/circles/:circleId/matches/:matchId` con score hero, progress dots, CTA prominente, feedback animato post-conferma (✓ pop + stato) e stato "contestata". Bottone "Conferma" nella lista naviga a questo componente invece di chiamare API diretto. Build verde (backend + frontend). **PROSSIMO PASSO:** revisione umana. Quando approvi: `/eq-approve US-005`.
**Reopened (2026-06-12):** Review umana: al click "Conferma" nella lista partite, navigare a un componente dedicato che mostri anche i risultati (set) e enfatizzi l'azione di conferma con feedback visivo.
**Blocked by:** US-004

**Story**
Come giocatore coinvolto in una partita, voglio confermare (o contestare) il risultato inserito, così che in classifica entrino solo dati convalidati da tutti.

**Demonstrates**
I 3 giocatori che non hanno inserito la partita la vedono in pending, la confermano uno a uno; alla quarta conferma la partita diventa `confirmed`.

**Acceptance Criteria**

- [ ] Ogni partecipante vede le proprie partite in attesa di conferma
- [ ] L'inserimento vale come conferma implicita dell'inseritore (1/4 alla creazione)
- [ ] La partita passa a `confirmed` solo quando tutti e 4 hanno confermato (da PRD §RF-3)
- [ ] Un partecipante può rifiutare il risultato → partita in stato `disputed`, esclusa dal ranking
- [ ] Un non-partecipante non può confermare né rifiutare
- [ ] Conferma doppia dello stesso giocatore è idempotente (nessun doppio conteggio)

**Out of scope**

- Flusso di risoluzione della disputa (correzione e re-invio) — per ora la disputa congela la partita
- Timeout automatico di conferma (dipende da open question PRD)

**Open questions**

- Cosa succede se uno dei 4 non conferma entro X ore? (open question già nel PRD: annullata o valida con 3/4?)

---

#### US-006: Notifica push di richiesta conferma

**Epic:** EP-002 | **Priority:** MEDIUM | **Story Points:** 3 | **Status:** REVIEW
**Review note (2026-06-12):** Backend: `FcmToken` entity + migration `AddFcmTokens`, `PushNotificationService`/`IFcmSender` (FirebaseAdmin) in `src/Golp.Api/Services/`, `PushEndpoints.cs` (`POST/DELETE /api/push/token`, `GET /api/push/vapid-public-key`), push fire-and-forget in `MatchEndpoints.CreateMatch`. Frontend: PWA (`@angular/pwa` + ngsw), `@angular/fire`, `push/push-notification.service.ts`, hook in `AuthService` (login/register → register, logout → unregister), `public/firebase-messaging-sw.js` (deep-link tap → `/circles/{circleId}/matches/{matchId}`). Test: 82 BE + 9 FE nuovi + 6 e2e verdi (2 fail FE pre-esistenti fuori scope). Reviewer APPROVE (2 iterazioni). ⚠️ Push reale richiede setup Firebase Console: vedi `docs/firebase-setup.md` (config in `environment.ts`, `firebase-messaging-sw.js` e user-secrets). > **PROSSIMO PASSO:** revisione umana. Quando approvi: `/eq-approve US-006`.
**Blocked by:** US-005

**Story**
Come giocatore coinvolto in una partita appena inserita, voglio ricevere una notifica push, così che possa confermare il risultato rapidamente e la classifica resti aggiornata.

**Demonstrates**
All'inserimento di una partita, i 3 giocatori da confermare ricevono una push; il tap apre la schermata di conferma.

**Acceptance Criteria**

- [ ] Push inviata ai 3 partecipanti che devono ancora confermare, non all'inseritore
- [ ] Il tap sulla notifica porta direttamente alla partita da confermare
- [ ] Permesso push negato dall'utente → l'app funziona comunque, la partita resta visibile nella lista pending
- [ ] Nessuna notifica duplicata per la stessa partita allo stesso giocatore

**Out of scope**

- Reminder periodici di sollecito (dipende dalla decisione sul timeout)
- Notifiche per eventi diversi dalla conferma (classifica, premi)

**Open questions**

- (nessuna)

---

## EP-003 — Ranking e Classifica

_Il valore core: trasformare partite confermate in una classifica oggettiva, aggiornata in tempo reale. (da PRD §RF-4, §Algoritmo di ranking)_

#### US-007: Calcolo rating ELO alla conferma partita

**Epic:** EP-003 | **Priority:** HIGH | **Story Points:** 5 | **Status:** DONE
**Approved (2026-06-12):** Review umana OK.
**Review note (2026-06-12):** `RatingService` in `src/Golp.Api/Services/` (formula da PRD §Algoritmo: amplifier 0.7 sul margine, K=32/48 per-player, clamp score_ratio [0.5,1.0]), 4 campi delta `int?` su `Match` + migration `AddMatchEloDeltas`, DI in `Program.cs` (rimosso `NoOpRatingService`). Già invocato da `ConfirmMatchAsync` (US-005) nella stessa transazione. Test: 9 unit + 3 integration in `src/Golp.Tests/`, suite backend 94/94 verde. Reviewer APPROVE. ⚠️ Nota: la formula nel piano US-007 era dedotta e divergeva dal PRD; implementata quella del PRD. > **PROSSIMO PASSO:** revisione umana. Quando approvi: `/eq-approve US-007`.
**Blocked by:** US-005

**Story**
Come Marco, voglio che il mio rating si aggiorni automaticamente appena la partita è confermata, così che la classifica rifletta i risultati reali senza arbitri.

**Demonstrates**
Alla quarta conferma di una partita, i rating dei 4 giocatori cambiano secondo la formula ELO del PRD; i delta sono persistiti per ogni giocatore.

**Acceptance Criteria**

- [ ] Alla transizione a `confirmed`, i rating dei 4 giocatori vengono ricalcolati con la formula del PRD §Algoritmo (team_rating = media, amplifier 0.7, K=32)
- [ ] K=48 per giocatori con meno di 15 partite confermate nel circolo (cold start)
- [ ] `score_ratio` calcolato sommando tutte le unità di entrambe le squadre, qualunque sia lo sport
- [ ] Il delta per giocatore viene salvato sulla partita (servirà per mostrare `+N pt`)
- [ ] Partite `pending` o `disputed` non producono alcun ricalcolo
- [ ] Lo stesso match confermato non viene mai conteggiato due volte (idempotenza)
- [ ] Rating iniziale di ogni giocatore = 1000 per circolo

**Out of scope**

- Esposizione della formula all'utente (decisione PRD: algoritmo opaco)
- Ricalcolo storico retroattivo in caso di dispute risolte
- Tuning dei parametri (amplifier, K) via config

**Open questions**

- (nessuna)

---

#### US-008: Classifica circolo in tempo reale

**Epic:** EP-003 | **Priority:** HIGH | **Story Points:** 3 | **Status:** DONE
**Approved (2026-06-12):** Review umana OK.
**Review note (2026-06-12):** Backend: `GetCircleLeaderboardAsync` in `CircleEndpoints.cs` (GET /circles/{id}/leaderboard, classified/unclassified split, rating DESC + confirmedMatches DESC). Frontend: `CircleLeaderboardComponent` standalone con podio top-3, lista classifica, sezione non classificati, highlight current user. Interfacce + `getLeaderboard()` in `circle.service.ts`. Route `/circles/:circleId/leaderboard` + link "Classifica" in MyCircles. Test: 101 BE verdi (7 nuovi integration) + 9 unit Angular + 4 E2E Playwright. Reviewer APPROVE. ⚠️ 2 test pre-esistenti in `circle-match-history.component.spec.ts` fuori scope.
**Blocked by:** US-007

**Story**
Come Marco, voglio vedere la classifica aggiornata del mio circolo, così che sappia la mia posizione reale rispetto agli altri.

**Demonstrates**
Dopo la conferma di una partita, la classifica del circolo riflette immediatamente i nuovi rating; l'utente vede la propria posizione evidenziata.
Tutti i giocatori iscritti al circolo che non hanno nessuna classifica pur avendo un punteggio iniziale pari a 1000 vanno mostrati in fondo alla classifica senza punteggio.

**Acceptance Criteria**

- [ ] La classifica mostra i membri del circolo ordinati per rating decrescente
- [ ] L'aggiornamento è visibile subito dopo la conferma della partita (da PRD §RF-4)
- [ ] La posizione del giocatore corrente è evidenziata
- [ ] A parità di rating, criterio di ordinamento secondario deterministico (es. numero partite giocate)
- [ ] Giocatori senza partite confermate visibili con rating 1000 (o sezione separata — vedi open question)
- [ ] Cambiando circolo, la classifica mostrata è solo quella del circolo selezionato

**Out of scope**

- Storico evoluzione classifica nel tempo
- Filtri per periodo (mese/anno — coperti dai premi in US-010)

**Open questions**

- I giocatori a 0 partite compaiono in classifica a 1000 pt o in una lista "non classificati"?

---

#### US-009: Esito partita con delta punti (+N / −N)

**Epic:** EP-003 | **Priority:** MEDIUM | **Story Points:** 2 | **Status:** DONE
**Approved (2026-06-12):** Review umana OK.
**Review note (2026-06-12):** Backend: `GetMatchesAsync` in `MatchEndpoints.cs` calcola `myDelta` dalle 4 posizioni player (null per pending/disputed/non-partecipante). Frontend: badge `+N pt` / `−N pt` in `circle-match-history.component.html` con CSS variables in `styles.scss` da style-tokens.json. Test: 108 BE verdi (7 nuovi integration in `MatchListEndpointTests.cs`) + 6 unit Angular + 3 E2E Playwright. Reviewer APPROVE. ⚠️ 2 test FE pre-esistenti fallenti fuori scope (AppComponent + confirm-click). > **PROSSIMO PASSO:** revisione umana. Quando approvi: `/eq-approve US-009`.
**Blocked by:** US-007

**Story**
Come Marco, voglio vedere quanti punti ho guadagnato o perso dopo ogni partita confermata, così che percepisca l'impatto del risultato senza dover capire l'algoritmo.

**Demonstrates**
Aperta una partita confermata, ogni giocatore vede il proprio `+12 pt` o `−8 pt`; la formula non è mai esposta (da PRD §UX).

**Acceptance Criteria**

- [ ] Dopo la conferma, ogni giocatore vede il proprio delta (`+N pt` / `−N pt`) sulla partita
- [ ] Il delta mostrato corrisponde esattamente a quello applicato al rating (stesso dato persistito in US-007)
- [ ] Nessun elemento UI espone formula, expected score o rating degli avversari usati nel calcolo
- [ ] Partite pending/disputed non mostrano alcun delta

**Out of scope**

- Spiegazione del calcolo ("perché +12?") — decisione PRD: algoritmo opaco
- Grafici andamento rating

**Open questions**

- (nessuna)

---

#### US-012: Rating ELO pesato su set per sport a set

**Epic:** EP-003 | **Priority:** MEDIUM | **Story Points:** 3 | **Status:** DONE
**Approved (2026-06-15):** Test 130/130 verdi, review automatica APPROVE. Pre-autorizzato dall'utente.
**Review note (2026-06-15):** `SportsConfig.SportDto`: nuovo campo `SetWeight = 0.0` (0.4 per padel/beachtennis). `RatingService`: `FindAsync(circle)` separato (no Include per evitare bug EF InMemory), branch blended `α×set_ratio + (1-α)×game_ratio` per `circle.Sets && SetWeight > 0`. Test: 13 unit (RatingServiceTests, +4 nuovi) + 4 integration (+1 blended e2e) = 130 totali verdi. Reviewer APPROVE.
**Blocked by:** US-007

**Story**
Come Marco, voglio che il mio rating rifletta non solo i game totali ma anche i set vinti, così che una vittoria netta 6-2 6-2 pesi di più rispetto a una tirata 4-6 6-4 7-6.

**Demonstrates**
Dopo una partita confermata di padel, il delta ELO assegnato tiene conto sia dei set vinti sia dei game totali: vincere 2-0 con margine ampio produce un delta maggiore rispetto a vincere 2-1 al super tiebreak.

**Acceptance Criteria**

- [ ] Per sport con `sets: true`, `score_ratio = α × set_ratio + (1-α) × game_ratio` (con `α = 0.4` default)
- [ ] `set_ratio = sets_vinti / (sets_vinti + sets_persi)` — un set è vinto dal team che ha segnato più punti/game in quel set
- [ ] `game_ratio` invariato: `games_vinti_totali / (games_vinti_totali + games_persi_totali)` (somma su tutti i set)
- [ ] Il super tiebreak (3° set a 10 punti) conta come set ordinario — sia in `set_ratio` che in `game_ratio`
- [ ] Per sport con `sets: false` (basket2v2, burraco) la formula rimane invariata — solo `game_ratio`
- [ ] Il parametro `α` (`set_weight`) è definito in `SportsConfig` per sport; 0.4 dove `sets: true`, ignorato dove `sets: false`
- [ ] Le partite già confermate prima di questa modifica non vengono ricalcolate

**Out of scope**

- Modifica al frontend (il form già invia i set separati per ciascuno)
- Variazione dei parametri `amplifier`, `K` o rating iniziale esistenti
- Visualizzazione del calcolo all'utente (rimane opaco per PRD)
- Ricalcolo retroattivo dei rating storici

**Open questions**

- (nessuna — scelte definite in analisi)

---

## EP-004 — Premi e Statistiche

_Gamification leggera e insight personali: giocatore del mese/anno e statistiche su compagni e avversari. (da PRD §RF-5, §RF-6)_

#### US-010: Giocatore del mese e dell'anno

**Epic:** EP-004 | **Priority:** MEDIUM | **Story Points:** 3 | **Status:** DONE
**Approved (2026-06-12):** Review umana OK.
**Review note (2026-06-12):** Backend: `CircleAward` entity + migration `AddCircleAwards`, `AwardsEndpoints.cs` (`GET /circles/{id}/awards` on-the-fly per mese/anno corrente, aggregazione in-memory con tie-break deterministico). Frontend: `CircleAwardsComponent` (ts/html/scss), interfacce `AwardWinner/AwardPeriodResult/CircleAwardsResponse` + `getAwards()` in `circle.service.ts`, route `/circles/:circleId/awards` + link "Premi" su ogni circle card. Test: 116 BE verdi (8 nuovi integration in `AwardsEndpointTests.cs`) + 4 unit Angular + 3 E2E Playwright. Reviewer APPROVE. ⚠️ 2 test FE pre-esistenti fallenti fuori scope.
**Blocked by:** US-007

**Story**
Come membro del circolo, voglio vedere chi è il giocatore del mese e dell'anno, così che la competizione resti viva anche per chi non è in cima alla classifica assoluta.

**Demonstrates**
Nella schermata del circolo compaiono il giocatore del mese corrente e dell'anno corrente; al cambio mese il conteggio mensile riparte (da PRD §RF-5).

**Acceptance Criteria**

- [ ] Il giocatore del mese è calcolato sui risultati delle sole partite confermate nel mese corrente
- [ ] Il giocatore dell'anno è calcolato sulle partite confermate nell'anno corrente
- [ ] Al cambio mese/anno il conteggio riparte (reset), lo storico dei premi passati resta consultabile
- [ ] Circolo senza partite nel periodo → nessun premiato mostrato, niente errori
- [ ] Il premio è per-circolo, mai cross-circolo

**Out of scope**

- Notifiche push per il premio
- Badge/trofei collezionabili

**Open questions**

- Metrica esatta del premio: maggior guadagno di rating nel periodo, o miglior win-rate con un minimo di partite?

---

#### US-011: Statistiche personali — compagni e avversari

**Epic:** EP-004 | **Priority:** MEDIUM | **Story Points:** 3 | **Status:** DONE
**Approved (2026-06-13):** Review umana OK.
**Review note (2026-06-13):** Backend: `StatsEndpoints.cs` (`GET /circles/{circleId}/stats/me`) con 2 query EF + aggregazione in-memory, soglia N=3, tie-break deterministico. Frontend: `CircleStatsComponent` (ts/html/scss) con ring SVG da style-tokens.json, stato vuoto + card "Dati non sufficienti" per null indipendenti. Route `/circles/:circleId/stats` + link "Stats" in MyCircles. Fix `proxy.conf.js` (bypass HTML navigation) necessario per E2E. Test: 125 BE verdi (+9 integration `PlayerStatsEndpointTests`) + 6 unit Angular + 3 E2E Playwright. Reviewer APPROVE. > **PROSSIMO PASSO:** revisione umana. Quando approvi: `/eq-approve US-011`.
**Blocked by:** US-007

**Story**
Come Marco, voglio sapere con quale compagno vinco di più e contro quale avversario faccio più fatica, così che capisca con chi gioco meglio (job-to-be-done del PRD).

**Demonstrates**
La schermata profilo mostra "miglior compagno" (win-rate più alto insieme) e "avversario più ostico" (win-rate più basso contro), calcolati sulle partite confermate del circolo.

**Acceptance Criteria**

- [ ] Miglior compagno = compagno con il win-rate più alto giocando in coppia con me (da PRD §RF-6)
- [ ] Avversario più ostico = avversario contro cui ho il win-rate più basso
- [ ] Solo partite `confirmed` entrano nel calcolo
- [ ] Statistiche per-circolo: cambiando circolo cambiano i dati
- [ ] Meno di N partite con un compagno/avversario → escluso dal calcolo per evitare statistiche su campione minimo (N da definire, default proposto: 3)
- [ ] Nessuna partita giocata → schermata con stato vuoto chiaro, niente errori

**Out of scope**

- Statistiche avanzate (trend, grafici, head-to-head dettagliato) — esplicitamente out of scope nel PRD
- Confronto statistiche tra giocatori

**Open questions**

- Soglia minima di partite per compagno/avversario: 3 va bene?

---

---

#### US-013: Conferma forzata del risultato da parte del proprietario del circolo

**Epic:** EP-002 | **Priority:** HIGH | **Story Points:** 3 | **Status:** DONE
**Approved (2026-06-15):** Review umana OK.
**Review note (2026-06-15):** Backend: `ForceConfirmMatchAsync` in `MatchEndpoints.cs` (`POST .../force-confirm`), 2 campi audit su `Match` (`ForceConfirmedById`/`At`), migration `AddMatchForceConfirmAudit`, `ownerId` aggiunto a `CircleSummary` DTO. Frontend: `isOwner` derivato da `getMyCircles()` in `CircleMatchHistoryComponent`, pulsante amber "Forza conferma" condizionale, `forceConfirm()` in `MatchService`. Test: 138 BE verdi (+8 `ForceConfirmMatchTests`) + 52 Angular (51 verdi, 1 pre-esistente stale in `app.component.spec.ts`). Reviewer APPROVE.
**Blocked by:** US-005

**Story**
Come proprietario del circolo, voglio poter confermare forzatamente il risultato di una partita quando uno o più dei 4 giocatori non risponde, così che le partite non restino bloccate indefinitamente in stato `pending` e il calcolo ELO possa procedere.

**Demonstrates**
Il proprietario vede nella lista partite del suo circolo quelle in stato `pending` con almeno un giocatore non confermato, può premere "Forza conferma" su una di esse, e la partita passa a `confirmed` innescando l'aggiornamento dei rating — esattamente come se tutti e 4 avessero confermato.

**Acceptance Criteria**

- [ ] Solo il proprietario del circolo (`owner_id`) può eseguire la conferma forzata; gli altri membri ricevono 403
- [ ] La conferma forzata è disponibile solo su partite in stato `pending`; su `confirmed` o `disputed` restituisce 400
- [ ] Dopo la conferma forzata la partita transita a `confirmed` e i rating ELO vengono aggiornati come da US-007
- [ ] L'azione è registrata (campo o log) per distinguerla dalla conferma organica 4/4 — audit trail minimo
- [ ] I giocatori coinvolti vedono la partita come `confirmed` nelle loro schermate senza ambiguità
- [ ] Multi-tenancy rispettata: il proprietario del circolo A non può forzare partite del circolo B

**Out of scope**

- Notifiche push ai giocatori non-confermanti al momento della forzatura (future)
- Timeout automatico con auto-conferma (separato, se mai)
- Riapertura di una partita già forzata

**Open questions**

- La forzatura deve richiedere una nota/motivazione obbligatoria, o basta il log silenzioso?

---

---

#### US-014: Generazione e condivisione del link di invito al circolo

**Epic:** EP-001 | **Priority:** HIGH | **Story Points:** 5 | **Status:** DONE
**Approved (2026-06-15):** Review umana OK.
**Review note (2026-06-15):** Backend: `GetInviteLinkAsync` in `CircleEndpoints.cs` (lazy token su `Circle.JoinCode`, MaxLength 32, migrazione `ExpandJoinCodeLength`). Frontend: `InviteDialogComponent` standalone + integrazione `MyCirclesComponent` (pulsante "Invita" condizionale, dialog overlay). Service: `getInviteLink()` + `InviteLinkResponse` in `circle.service.ts`. Test: 143 BE verdi (+5 integration `InviteLinkEndpointTests`) + 56/57 Angular (+5 unit) + 3 E2E Playwright. Reviewer APPROVE.
**Blocked by:** -

**Story**
Come creatore di un circolo, voglio generare un link di invito e condividerlo tramite email o copiarlo negli appunti, così che i giocatori che voglio possano unirsi al mio circolo.

**Demonstrates**
Dal dettaglio del circolo, il creatore preme "Invita" e ottiene una dialog con il link generato, il pulsante "Copia link" e il pulsante "Invia via email" (mailto). Il link è unico per circolo e non scade.

**Acceptance Criteria**

- [ ] Solo il creatore del circolo vede il pulsante "Invita"
- [ ] Premendo "Invita" si apre una dialog/modal con il link di invito
- [ ] `GET /circles/{circleId}/invite-link` restituisce `{ inviteToken: "..." }` (403 se non creatore)
- [ ] Il token di invito è persistito sul circolo (`InviteToken` generato una volta sola, stabile)
- [ ] Pulsante "Copia link" copia negli appunti l'URL completo (`<origin>/join?token=<token>`)
- [ ] Pulsante "Invia via email" apre `mailto:?subject=...&body=<link>` precompilato
- [ ] Il link funziona anche su mobile (WhatsApp, altri — è un URL standard da incollare)

**Out of scope**

- Scadenza o revoca del link (il token è permanente per ora)
- Invito diretto via app (notifica push, SMS)
- Gestione di più token attivi contemporaneamente

**Open questions**

- Il token va rigenerato se il creatore preme di nuovo "Invita", o è sempre lo stesso?

---

#### US-015: Registrazione e auto-iscrizione al circolo tramite link di invito

**Epic:** EP-001 | **Priority:** HIGH | **Story Points:** 5 | **Status:** DONE
**Approved (2026-06-15):** Review umana OK.
**Blocked by:** US-014
**Review note (2026-06-15):** Codice in `src/Golp.Api/Endpoints/CircleEndpoints.cs`, `frontend/golp-app/src/app/circles/join-circle/`, `frontend/golp-app/src/app/auth/`. Test in `src/Golp.Tests/Integration/JoinByTokenEndpointTests.cs`, `frontend/golp-app/src/app/circles/join-circle/join-circle.component.spec.ts`, `frontend/golp-app/e2e/join-invite.spec.ts`. Reviewer APPROVE. 148 BE + 60 FE tests green, 3/3 E2E join-invite pass. > **PROSSIMO PASSO:** revisione umana. Quando approvi, lancia `/eq-approve US-015`.

**Story**
Come utente che ha ricevuto un link di invito, voglio registrarmi al sistema e trovarmi automaticamente nel circolo a cui sono stato invitato, così che non devo cercare e richiedere manualmente l'accesso.

**Demonstrates**
L'utente apre `/join?token=<token>`, viene reindirizzato alla registrazione (o al login se già ha un account), e dopo l'autenticazione è iscritto al circolo con rating iniziale 1000 e vede il circolo nella propria lista.

**Acceptance Criteria**

- [ ] La rotta `/join?token=<token>` è accessibile senza autenticazione
- [ ] Se l'utente non è autenticato, viene reindirizzato a `/register?inviteToken=<token>` (token preservato)
- [ ] Se l'utente è già autenticato, viene reindirizzato a `/login?inviteToken=<token>`
- [ ] `POST /circles/join` con body `{ inviteToken }` aggiunge l'utente come membro (rating 1000) se non già membro
- [ ] Dopo il join, l'utente viene reindirizzato alla pagina del circolo appena unito
- [ ] Token non valido → messaggio d'errore chiaro ("Link non valido o scaduto")
- [ ] Un utente già membro che usa lo stesso link riceve messaggio informativo, non errore

**Out of scope**

- Approvazione manuale del creatore prima dell'ingresso
- Limite al numero di iscrizioni via link

**Open questions**

- Se il token è valido ma il circolo è stato eliminato, cosa mostrare?

---

---

## EP-005 — Configurazione e Amministrazione

_Funzionalità per gestire la piattaforma senza dover rilasciare nuove versioni: sport, parametri ELO, configurazioni operative._

#### US-016: Sport configurabili da database

**Epic:** EP-005 | **Priority:** HIGH | **Story Points:** 5 | **Status:** TODO
**Blocked by:** -

**Story**
Come amministratore della piattaforma, voglio gestire l'elenco degli sport supportati tramite database, così che possa aggiungere o modificare sport senza rilasciare una nuova versione della PWA.

**Demonstrates**
Un amministratore aggiunge un nuovo sport (`padel 4v4`) direttamente sul DB. Senza alcun deploy, l'endpoint `/sports` lo restituisce già e i giocatori possono selezionarlo durante la registrazione di una partita.

**Acceptance Criteria**

- [ ] Esiste una tabella `Sports` nel DB con colonne: `Id`, `Key`, `DisplayName`, `PointUnit`, `Sets`, `TeamSize`, `IsActive`
- [ ] L'endpoint `GET /sports` legge da DB (non da `SportsConfig` statico) e restituisce solo sport con `IsActive = true`
- [ ] `SportsConfig` statico viene rimosso o deprecato: nessun endpoint lo usa più direttamente
- [ ] Una migration EF popola la tabella con gli sport attualmente definiti in `SportsConfig` (dati iniziali idempotenti)
- [ ] Il frontend riceve e visualizza correttamente la lista sport proveniente da DB, senza modifiche al contratto API esistente
- [ ] La validazione sport nelle partite (`MatchEndpoints`) usa i valori da DB, non dalla classe statica

**Out of scope**

- UI di amministrazione per gestire sport (CRUD via interfaccia grafica)
- Autenticazione/autorizzazione per la modifica della tabella Sports (gestita solo via accesso diretto al DB)
- Parametri ELO (K, amplifier) in database

**Open questions**

- La colonna `Key` deve essere stabile (usata come FK nelle partite esistenti) o è solo display? Verificare se il dominio usa già una stringa-chiave o un ID numerico per riferirsi agli sport nei match registrati.

---

#### US-017: Pagina spiegazione algoritmo ELO e simulatore partita

**Epic:** EP-003 | **Priority:** MEDIUM | **Story Points:** 5 | **Status:** DONE
**Approved (2026-06-23):** Review umana OK.
**Review note (2026-06-15):** Backend: `RatingService.ComputeDeltas` (public static), `SimulateEndpoints.cs` (`POST /simulate-match` pubblico), `Program.cs`. Frontend: `elo-info/` (service + component + route), link in leaderboard e dashboard. Test: `SimulateEndpointTests.cs` (10 test). Reviewer APPROVE. > **PROSSIMO PASSO:** revisione umana. Quando approvi, lancia `/eq-approve US-017`.
**Blocked by:** -

**Story**
Come giocatore, voglio leggere una spiegazione semplice di come viene calcolato il mio rating e simulare il risultato di una partita ipotetica con i punteggi che scelgo io, così che possa capire l'impatto delle mie vittorie e sconfitte senza dover aspettare una partita reale.

**Demonstrates**
Fabio è in classifica con 1050 punti. Clicca "Come funziona il rating?" dalla pagina classifica. Legge la spiegazione in linguaggio semplice. Inserisce: sé stesso 1050, compagno 980, avversari 1100 e 1090, risultato vittoria. La pagina mostra "+18 punti" per lui e "+15 per il compagno". Torna alla classifica.

**Acceptance Criteria**

- [ ] Esiste una route `/elo-info` (o simile) con una pagina standalone raggiungibile senza login
- [ ] La pagina contiene una sezione testuale che spiega l'algoritmo ELO in linguaggio non tecnico (cosa è il rating iniziale, come sale/scende, ruolo del K dinamico per i primi 15 match)
- [ ] La pagina contiene un form simulatore con 4 campi rating (proprio, compagno, avversario 1, avversario 2) e un selettore "modalità risultato"
- [ ] La modalità risultato permette di scegliere tra "Risultato unico" (due campi: punti mia squadra / punti avversari) e "Per set" (coppie di punteggio aggiungibili dinamicamente con + Aggiungi set)
- [ ] Dopo submit il simulatore mostra la variazione di rating prevista per il team vincente e per il team perdente, calcolata lato client con la stessa formula ELO usata dal backend (score_ratio dal punteggio reale inserito, non fisso)
- [ ] I campi rating hanno validazione: valori numerici interi tra 0 e 3000
- [ ] I campi punteggio partita hanno validazione: valori interi ≥ 0, almeno un set/risultato inserito, il totale dei punti deve essere > 0
- [ ] Dalla pagina classifica è presente un link visibile "Come funziona il rating?" che porta a `/elo-info`
- [ ] Dalla dashboard è presente un link/card "Simula una partita" che porta a `/elo-info`

**Out of scope**

- Storico simulazioni salvate
- Simulazione multi-round o tornei
- Personalizzazione parametri ELO (K, amplifier) da UI
- Login obbligatorio per accedere alla pagina

**Open questions**

- -

---

#### US-018: Aggiunta manuale di un giocatore al circolo da parte del proprietario

**Epic:** EP-001 | **Priority:** HIGH | **Story Points:** 5 | **Status:** DONE
**Approved (2026-06-23):** Review umana OK.
**Review note (2026-06-19):** Backend in `src/Golp.Api/Endpoints/CircleEndpoints.cs` (`POST /circles/{id}/members`, owner-only, 3 branch: lookup esistente, conferma+add, crea pending), `IEmailService`/`DevelopmentEmailService.cs` (+2 metodi), fix `AuthEndpoints.LoginAsync` (BCrypt.Verify lanciava eccezione su hash vuoto invece di false). Frontend in `frontend/golp-app/src/app/circles/add-member-dialog/` (componente standalone 4-step), `circle.service.ts` (+metodo), `my-circles.component.*` (bottone owner-only "+ Giocatore"). Test: 9 integration in `CircleIntegrationTests.cs` (`AddMemberEndpointTests`), 7 unit Angular, 2 E2E Playwright (`e2e/add-member.spec.ts`). Reviewer APPROVE — no critical aperti. Suite completa: 158/167 BE (9 fail pre-esistenti in `SimulateEndpointTests`, confermato su `main` via stash), 62/69 FE (7 fail pre-esistenti AuthService/AppComponent). > **PROSSIMO PASSO:** revisione umana. Quando approvi: `/eq-approve US-018`.
**Blocked by:** -

**Story**
Come proprietario di un circolo, voglio registrare direttamente un nuovo giocatore inserendo i suoi dati, così che possa aggiungerlo al circolo senza dover passare da un link di invito.

**Demonstrates**
Il proprietario, dalla pagina di gestione del circolo, inserisce email e nome di un giocatore. Se l'email non esiste già nel sistema, viene creato un nuovo account e il giocatore riceve una email con le istruzioni per impostare la password e accedere. Se l'email corrisponde a un utente già registrato, il proprietario vede il nome associato e deve confermare prima che il giocatore venga aggiunto come membro del circolo (rating iniziale 1000); il giocatore riceve una email di notifica che è stato aggiunto al circolo.

**Acceptance Criteria**

- [ ] Solo il proprietario del circolo può accedere alla funzione di aggiunta manuale giocatore (autorizzazione lato API)
- [ ] Il form richiede almeno email; se l'email non è già registrata, richiede anche nome (e altri campi minimi richiesti dalla registrazione)
- [ ] Se l'email esiste già nel DB, l'API restituisce il nome associato e NON crea un nuovo utente; il frontend mostra il nome e chiede conferma esplicita prima di procedere
- [ ] Alla conferma, se l'utente esiste già: viene aggiunto come `CircleMembership` del circolo con rating iniziale 1000 (se non già membro)
- [ ] Se l'email non esiste: viene creato un nuovo utente con stato "in attesa di attivazione" (password non impostata) e aggiunto come membro del circolo con rating iniziale 1000
- [ ] Nel caso di nuovo utente, viene inviata una email con link/istruzioni per impostare la password e accedere (riuso flusso reset/attivazione password esistente)
- [ ] Nel caso di utente già esistente, viene inviata una email di notifica che informa dell'aggiunta al circolo
- [ ] Se l'utente (esistente) è già membro del circolo, l'azione non duplica la membership e mostra messaggio informativo invece di errore
- [ ] L'email del nuovo giocatore deve passare la stessa validazione di formato usata in registrazione

**Out of scope**

- Import massivo di giocatori (CSV/bulk)
- Possibilità per il giocatore aggiunto di rifiutare l'iscrizione al circolo
- Modifica successiva dell'email del giocatore aggiunto

**Open questions**

- L'email in ambiente di sviluppo passa da `DevelopmentEmailService` (solo console): verificare se basta per questa storia o serve già SMTP reale.
- Il "nome" per un nuovo utente creato qui è obbligatorio o può restare vuoto fino al primo login dell'utente stesso?

---

#### US-019: Sessione lunga tramite refresh token

**Epic:** EP-001 | **Priority:** HIGH | **Story Points:** 5 | **Status:** DONE
**Approved (2026-06-23):** Review umana OK.
**Review note (2026-06-19):** Backend: entità `RefreshToken` + migration `AddRefreshTokens`, `IRefreshTokenService`/`RefreshTokenService.cs` (issue/rotate con reuse-detection/revoke), `AuthEndpoints.cs` (+`POST /auth/refresh`, +`POST /auth/logout`, register/login ritornano `{accessToken, refreshToken}`, hook revoca-tutto su cambio password). Config `Jwt:RefreshTokenExpiryDays` (default 90). Frontend: `AuthService` (store coppia token, `refresh()`, `logout()` via API), `auth.interceptor.ts` (retry automatico su 401). Test: 2 unit `RefreshTokenServiceTests`, 8 integration in `AuthIntegrationTests.cs`, 4 unit `auth.interceptor.spec.ts`. Reviewer APPROVE — no critical aperti. Suite completa: 167/176 BE (9 fail pre-esistenti `SimulateEndpointTests`), 65/74 FE (9 fail pre-esistenti per `environment.ts` con apiUrl hardcoded a server live, non relativo — confermato non causato da questa storia). > **PROSSIMO PASSO:** revisione umana. Quando approvi: `/eq-approve US-019`.
**Blocked by:** US-001

**Story**
Come giocatore che ha installato l'app come PWA in home screen, voglio restare loggato per un periodo lungo (90 giorni di default) usando l'app anche una sola volta in quel periodo, così che non debba rifare login continuamente come accade oggi con la scadenza fissa di 60 minuti.

**Demonstrates**
Login emette un access token JWT di breve durata (es. 1h, come oggi) e un refresh token long-lived persistito in DB. Quando l'access token scade, il frontend lo rinnova automaticamente tramite il refresh token senza richiedere credenziali. Ogni rinnovo valido estende la scadenza del refresh token di altri N giorni (sliding window). Se l'utente non usa l'app per più di N giorni, il refresh token scade e serve nuovo login. Il proprietario dell'account può revocare le sessioni attive (logout singolo device o da tutti i device); un cambio password revoca tutti i refresh token esistenti.

**Acceptance Criteria**

- [ ] Login e registrazione restituiscono access token (JWT, breve durata) + refresh token (long-lived)
- [ ] Endpoint `POST /auth/refresh` scambia un refresh token valido con un nuovo access token; il refresh token stesso viene rinnovato (scadenza estesa di N giorni dal momento dell'uso)
- [ ] Durata refresh token configurabile via `appsettings.json` (es. `Jwt:RefreshTokenExpiryDays`), default 90
- [ ] Refresh token scaduto, già usato/revocato, o non trovato → `401`, frontend reindirizza al login
- [ ] Logout invalida (revoca) il refresh token della sessione corrente lato server, non solo lato client
- [ ] Cambio password revoca tutti i refresh token attivi dell'utente (logout da tutti i device)
- [ ] Per ogni refresh token sono tracciati `CreatedAt`, `LastUsedAt`, `UserAgent` (per stima utenti/device attivi)
- [ ] Frontend intercetta `401` da access token scaduto, tenta refresh automatico in background, poi ripete la richiesta originale; se il refresh fallisce, reindirizza al login
- [ ] Nessuna eccezione alle regole di isolamento multi-tenant per `circle_id` introdotta dal nuovo schema di token

**Out of scope**

- Scadenza assoluta indipendente dall'uso (no limite massimo "comunque rilogin dopo X tempo")
- Dashboard utente per visualizzare/gestire le proprie sessioni/device attivi (solo revoca implicita su logout/cambio password)
- Sistema di analytics completo (funnel, eventi): il tracciamento serve solo a stimare utenti/dispositivi attivi

**Open questions**

- Rilevazione riuso di un refresh token già consumato (rotazione con detection furto): da includere in questa storia o rimandata a una storia successiva di hardening?

---

#### US-020: Email su template riutilizzabili + notifiche email per richiesta conferma e contestazione partita

**Epic:** EP-002 | **Priority:** MEDIUM | **Story Points:** 5 | **Status:** REVIEW
**Review note (2026-06-19):** Backend: `Services/IEmailTemplateRenderer.cs`/`EmailTemplateRenderer.cs` (rendering placeholder+layout), `EmailTemplates/*.html` (5 template + layout condiviso), `SmtpEmailService.cs`/`DevelopmentEmailService.cs` migrati ai template (zero HTML inline), `IEmailService` esteso con `SendMatchConfirmationRequestEmailAsync`/`SendMatchDisputedEmailAsync`. `MatchEndpoints.cs`: email ai 3 destinatari su creazione partita (oltre al push esistente) e all'owner su dispute, entrambe fire-and-forget con try/catch per-destinatario (un fallimento non blocca gli altri né il flusso principale). Test: 3 unit `EmailTemplateRendererTests`, regressione 38 test esistenti, 4 nuovi integration test (conferma partita, dispute, 2 di resilienza a fallimento SMTP). Reviewer APPROVE — no critical aperti. Suite completa: 174/183 BE (9 fail pre-esistenti `SimulateEndpointTests`, non toccati da questa storia). > **PROSSIMO PASSO:** revisione umana. Quando approvi: `/eq-approve US-020`.
**Blocked by:** -

**Story**
Come team di sviluppo, voglio che le email siano generate da template HTML riutilizzabili invece che da stringhe inline nel codice, e come giocatore voglio ricevere una email (oltre alla notifica push) quando devo confermare una partita o quando una partita viene contestata, così che il branding sia consistente e gestibile senza toccare codice C#, e nessuna richiesta di conferma passi inosservata per chi non ha la PWA installata o le notifiche push attive.

**Demonstrates**
Le 3 email esistenti (reset password, attivazione giocatore, notifica aggiunta circolo) sono renderizzate da file template HTML in `src/Golp.Api/EmailTemplates/` con placeholder, non più da stringhe C# inline. Quando viene creata una partita, i 4 partecipanti (esclusi i confirmatori automatici se previsti) ricevono sia la push esistente sia una nuova email di richiesta conferma. Quando una partita passa a stato "disputed", il proprietario del circolo riceve una email di notifica.

**Acceptance Criteria**

- [ ] Esiste un meccanismo di rendering template (es. `IEmailTemplateRenderer`) che carica un file HTML da `EmailTemplates/` e sostituisce placeholder (`{{Nome}}`) con valori a runtime
- [ ] Le 3 email esistenti (`SendPasswordResetEmailAsync`, `SendCircleActivationEmailAsync`, `SendAddedToCircleNotificationAsync`) usano il nuovo meccanismo di template, nessun HTML inline rimane in `SmtpEmailService.cs`/`DevelopmentEmailService.cs`
- [ ] Esiste un layout/header-footer condiviso tra i template per branding consistente (logo/nome GOLP, colori base)
- [ ] Alla creazione di una partita (stesso punto in cui oggi parte la push in `MatchEndpoints.cs`), ai partecipanti che devono confermare viene inviata anche una email con link diretto alla pagina di conferma
- [ ] Quando una partita passa a stato `disputed`, il proprietario del circolo riceve una email di notifica con riferimento alla partita
- [ ] Se l'invio email fallisce (SMTP non raggiungibile), il flusso applicativo principale (creazione partita / dispute) non viene bloccato — l'errore è loggato, non propagato all'utente
- [ ] `DevelopmentEmailService` (fallback console, usato quando SMTP non configurato) supporta gli stessi template, stampa il contenuto risolto in console invece di inviarlo

**Out of scope**

- Preferenze/opt-out email per utente (tutte le email previste restano obbligatorie, nessuna UI impostazioni) — deciso in AN-001
- Notifica email per "conferma forzata dal proprietario" (US-013) o "partita confermata con esito" — non richieste in questa storia
- Localizzazione multi-lingua dei template (solo italiano)
- Giocatore del mese/anno via email — storia separata (richiede anche scheduler), vedi AN-001

**Open questions**

- (nessuna — risolte in AN-001 durante discussione 2026-06-19)

---

#### US-021: Notifica email automatica giocatore del mese/anno

**Epic:** EP-004 | **Priority:** LOW | **Story Points:** 5 | **Status:** TODO
**Blocked by:** US-020

**Story**
Come giocatore che vince il premio "giocatore del mese" o "giocatore dell'anno" in un circolo, voglio ricevere una email automatica che me lo comunica, così che non debba scoprirlo per caso aprendo la pagina premi.

**Demonstrates**
Un job schedulato calcola, alla chiusura di ogni mese/anno, il vincitore di ciascun circolo (stessa logica oggi esposta on-demand da `GET /circles/{id}/awards`) e invia una email al vincitore con il periodo e il risultato. Il job gira una sola volta per periodo per circolo, anche se eseguito più volte (idempotente).

**Acceptance Criteria**

- [ ] Esiste un meccanismo di esecuzione schedulata (es. `IHostedService`/`BackgroundService`) che gira periodicamente (es. ogni notte) e verifica se è il primo giorno di un nuovo mese/anno per cui calcolare il vincitore del periodo precedente
- [ ] Per ogni circolo con almeno 1 partita confermata nel periodo, calcola il vincitore con la stessa logica di `GetAwardsAsync` esistente
- [ ] Il vincitore riceve una email con periodo (es. "Giugno 2026"), nome circolo, e il risultato (net gain / partite giocate)
- [ ] Se un circolo non ha un vincitore per il periodo (nessuna partita confermata), nessuna email viene inviata per quel circolo
- [ ] Il job è idempotente: se eseguito più volte per lo stesso periodo/circolo, non invia email duplicate (es. tramite tabella di stato "periodo già processato")
- [ ] Se l'invio email fallisce per un circolo, gli altri circoli del batch continuano a essere processati (un fallimento non blocca gli altri)

**Out of scope**

- Notifica per "secondo posto" o classifiche complete via email (solo il vincitore)
- UI per configurare l'orario/frequenza del job (valore fisso in configurazione)
- Esecuzione retroattiva per periodi passati già conclusi prima del rilascio di questa storia

**Open questions**

- Il job deve girare su un thread in-process (`BackgroundService` nello stesso processo API) o un processo separato (Azure Function/WebJob)? Impatta deploy su Azure App Service esistente — da chiarire in fase di piano tecnico.

---

#### US-022: Numero di versione visibile in login e dashboard

**Epic:** EP-005 | **Priority:** LOW | **Story Points:** 3 | **Status:** DONE
**Approved (2026-06-22):** Review umana OK.
**Review note (2026-06-22):** Codice in `frontend/golp-app/scripts/generate-version.js` (generator), `frontend/golp-app/src/app/shared/version/app-version.component.ts` (componente condiviso + test), wiring in `scripts/deploy-frontend.ps1`. Verificato manualmente: determinismo stesso-commit (doppia esecuzione → stesso output), build+zip end-to-end con version.ts rigenerato (v38/d11eb98), unit test 2/2 verdi. Reviewer APPROVE — no critical aperti. > **PROSSIMO PASSO:** revisione umana. Quando approvi, lancia `/eq-approve US-022` (o aggiorna manualmente lo status a `DONE`).
**Blocked by:** -

**Story**
Come amministratore/sviluppatore del progetto, voglio che la schermata di login e la dashboard mostrino un numero di versione del software, così che possa verificare rapidamente quale build è in esecuzione su ciascun ambiente (prod/test) senza dover ispezionare manualmente il deploy.

**Demonstrates**
Login e dashboard mostrano una piccola label di versione (es. in footer), non invasiva. La versione è derivata in modo deterministico dall'ultimo commit (hash o data) tramite un algoritmo riconoscibile da chi conosce la logica ma non deducibile dall'utente finale guardando solo il numero. Lo stesso commit produce sempre lo stesso numero di versione.

**Acceptance Criteria**

- [ ] La schermata di login mostra un numero di versione (es. in un footer discreto)
- [ ] La dashboard mostra lo stesso numero di versione
- [ ] Il numero di versione è generato automaticamente ad ogni build, senza intervento manuale (es. da script di build/CI, non da un valore hardcoded da aggiornare a mano)
- [ ] Lo stesso commit produce sempre lo stesso numero di versione, in build diverse (deterministico)
- [ ] Build di commit diversi producono numeri di versione diversi, così è possibile verificare se due ambienti (prod/test) eseguono lo stesso codice
- [ ] Il formato mostrato all'utente è semplice da leggere e comunicare (es. pattern tipo semver o build counter), senza esporre direttamente hash o data del commit

**Out of scope**

- Changelog visibile all'utente collegato alla versione
- Versioning semantico "vero" con incrementi manuali di major/minor per breaking change
- Sincronizzazione di versione tra frontend e backend come requisito vincolante (possono avere numeri propri, da chiarire in piano tecnico)

**Open questions**

- Algoritmo esatto di trasformazione commit→numero versione (idee da valutare in `/eq-plan`: contatore commit via `git rev-list --count HEAD`, data ultimo commit in formato `YY.MM.DD`, oppure hash troncato mappato a base36/base62)
- Frontend (build Angular) e backend (assembly .NET) mostrano versioni indipendenti o devono combaciare in un unico numero condiviso?

---

#### US-023: Aggiornamento automatico dei client dopo un rilascio

**Epic:** EP-005 | **Priority:** LOW | **Story Points:** 5 | **Status:** DONE
**Approved (2026-06-22):** Review umana OK.
**Review note (2026-06-22):** Codice in `frontend/golp-app/src/app/shared/update/` (`AppUpdateService` + `AppUpdateBannerComponent`, con test), wiring in `app.component.ts` (visibilitychange + NavigationEnd), cache hardening in `frontend/golp-app/public/web.config`. 12 unit test nuovi (6 service + 3 banner + 2 wiring AppComponent + build verificato). Test suite: 9 fail pre-esistenti (non toccati da questa storia), 78 verdi. Reviewer APPROVE — no critical aperti. Nota non bloccante: header HTTP reali (`index.html` no-cache, bundle cache lunga) verificati solo a livello build/struttura XML, non su IIS reale — da confermare al primo deploy su Testing. > **PROSSIMO PASSO:** revisione umana. Quando approvi, lancia `/eq-approve US-023` (o aggiorna manualmente lo status a `DONE`).
**Blocked by:** -

**Story**
Come Marco (giocatore amatoriale che usa la PWA installata sul telefono) voglio che l'app mi avvisi quando è disponibile una nuova versione e mi lasci aggiornare con un click, così che dopo un rilascio non resti bloccato su una versione vecchia senza saperlo. Come amministratore/sviluppatore voglio che il meccanismo di cache HTTP non impedisca ai client di scoprire le nuove versioni.

**Demonstrates**
Dopo un deploy, un utente che ha l'app già aperta (o la riapre dopo averla lasciata in background) vede entro breve tempo un banner non invasivo "Nuova versione disponibile — Aggiorna"; al click, l'app si aggiorna e mostra il nuovo numero di versione (US-022). Nessun reload automatico forzato senza preavviso.

**Acceptance Criteria**

- [ ] `index.html` viene servito da IIS con cache non persistente (no-cache/validazione), così che un client non resti bloccato su un `index.html` vecchio che referenzia bundle non più esistenti
- [ ] I bundle con hash di contenuto (JS/CSS generati da Angular CLI) hanno una politica di cache lunga esplicita (gli hash già garantiscono invalidazione automatica ad ogni build diversa)
- [ ] Quando l'app torna in foreground (utente riapre il tab/la PWA dopo averla lasciata in background) o l'utente naviga tra le pagine principali, l'app verifica se è disponibile una versione più recente
- [ ] Se una versione più recente è pronta, l'utente vede un banner/avviso non bloccante (niente `confirm()`/`alert()`) con un'azione esplicita per aggiornare
- [ ] Al click sull'azione di aggiornamento, l'app attiva la nuova versione e si ricarica, mostrando il nuovo numero di versione (coerente con US-022)
- [ ] Se non è disponibile nessuna versione nuova, non viene mostrato nessun banner (nessun falso positivo)
- [ ] Il meccanismo di check non genera errori bloccanti se l'app è offline al momento del controllo

**Out of scope**

- Reload automatico forzato senza interazione dell'utente (si avvisa, non si interrompe il lavoro in corso)
- Polling continuo a intervalli fissi in background (il check è legato a eventi: ritorno in foreground / navigazione tra pagine principali, non un timer sempre attivo)
- Versione del backend (.NET API) — fuori scope come già per US-022
- Un pulsante manuale "Aggiorna applicazione" sempre visibile indipendente dal banner (può essere una storia futura se il banner via eventi si rivela insufficiente)

**Open questions**

- Posizione e stile del banner di avviso: va deciso in fase di piano/design (toast in alto, barra in dashboard, badge vicino al numero di versione di US-022).
- `web.config` (`frontend/golp-app/public/web.config`) oggi non ha nessuna direttiva di cache: va aggiunta una regola che tenga `index.html` sempre non-cacheable e dia cache lunga ai bundle hashati — la sintassi IIS esatta (override per singolo file dentro `<staticContent>`) va verificata in fase di piano tecnico.
- Il meccanismo si basa su `SwUpdate` di `@angular/service-worker` (già registrato in `app.config.ts`, mai usato esplicitamente finora) — va confermato che la versione installata supporti l'API `versionUpdates`/`checkForUpdate()`/`activateUpdate()` usata nel piano.

---

#### US-024: Guida all'installazione della PWA per nuovi utenti da browser

**Epic:** EP-001 | **Priority:** MEDIUM | **Story Points:** 5 | **Status:** DONE
**Approved (2026-06-23):** Review umana OK. Status backlog non aggiornato durante lo sviluppo (commit a404cca "us-024 done"); codice verificato presente: `frontend/golp-app/src/app/shared/pwa-install/` (`pwa-install-banner`, `pwa-install-guide`, `pwa-install-steps`, `pwa-install.service`, `pwa-platform.service`, ognuno con spec), e2e in `frontend/golp-app/e2e/pwa-install.spec.ts`.
**Blocked by:** -

**Story**
Come Marco (nuovo utente che apre Golp per la prima volta da un browser mobile), voglio che mi venga spiegato che l'app funziona meglio se installata sul telefono, e voglio una mini guida su misura per il mio browser/sistema operativo, così che possa installarla senza dover cercare istruzioni altrove.

**Demonstrates**
Al primo accesso da browser (non da PWA già installata) su un dispositivo mobile, l'utente vede un messaggio/banner che spiega il beneficio dell'installazione. Se l'utente chiede di vedere la guida, gli viene mostrata una mini guida con gli step specifici per il suo browser (es. Safari iOS vs Chrome Android) e sistema operativo (iOS vs Android), non un'istruzione generica uguale per tutti.

**Acceptance Criteria**

- [ ] Al primo accesso via browser (non PWA già installata, rilevabile es. tramite `display-mode: standalone`) viene mostrato un messaggio non bloccante che invita all'installazione, spiegando il beneficio
- [ ] Il messaggio non viene più mostrato nelle sessioni successive una volta che l'utente l'ha chiuso o ha già installato l'app (no banner ripetuto ad ogni visita)
- [ ] Il sistema rileva browser (es. Safari, Chrome, Samsung Internet, Firefox) e sistema operativo del dispositivo (iOS, Android) lato client
- [ ] In base a browser+OS rilevati, viene mostrata una mini guida con gli step corretti per quella combinazione (es. iOS Safari: "Condividi → Aggiungi a Home"; Android Chrome: prompt nativo `beforeinstallprompt` o istruzioni "Menu → Installa app")
- [ ] Se il browser supporta il prompt nativo di installazione (`beforeinstallprompt` su Chrome/Edge Android), la guida offre anche un'azione diretta che lo attiva, oltre alla spiegazione manuale
- [ ] Se browser/OS non sono supportati per l'installazione PWA (combinazione non riconosciuta), non viene mostrata una guida errata o fuorviante — fallback a un messaggio generico o nessun messaggio
- [ ] Su desktop il comportamento è gestito esplicitamente (mostrare guida desktop, oppure non mostrare nulla — da chiarire in piano tecnico) e non mostra per errore istruzioni pensate per mobile

**Out of scope**

- Tracciamento/analytics di quanti utenti installano effettivamente l'app
- Incentivi o reminder periodici post-rifiuto (un solo invito iniziale, non campagna ricorrente)
- Installazione automatica o forzata senza azione esplicita dell'utente

**Open questions**

- Su desktop: mostrare comunque l'invito (con istruzioni desktop) o sopprimerlo del tutto? Da decidere in `/eq-plan`.
- Se l'utente chiude il banner senza installare, va riproposto dopo N giorni/visite, o mai più nella stessa sessione browser/dispositivo?
- Lista browser/OS da supportare esplicitamente nella mini guida (almeno Safari iOS, Chrome Android; Samsung Internet e Firefox Android da valutare in piano).

---

#### US-025: Il proprietario del circolo può registrare partite cui non partecipa

**Epic:** EP-002 | **Priority:** MEDIUM | **Story Points:** 3 | **Status:** DONE
**Approved (2026-06-22):** Review umana OK.
**Review note (2026-06-22):** Codice in `src/Golp.Api/Endpoints/MatchEndpoints.cs` (`CreateMatchAsync`: eccezione owner + conferma implicita condizionale), test in `src/Golp.Tests/Integration/MatchIntegrationTests.cs` (2 nuovi + 1 esistente che copre il caso non-owner). Suite: 176 verdi, 9 fail pre-esistenti in `SimulateEndpointTests` (non toccati, confermati identici su main via stash). Reviewer APPROVE — no critical aperti. Deviazione dal piano: errore resta `400` (non `403`) per non rompere test esistente. > **PROSSIMO PASSO:** revisione umana. Quando approvi, lancia `/eq-approve US-025` (o aggiorna manualmente lo status a `DONE`).
**Blocked by:** -

**Story**
Come proprietario di un circolo, voglio poter registrare una partita giocata tra 4 membri anche quando io stesso non sono uno dei 4 giocatori, così che possa inserire i risultati per conto del gruppo senza dover chiedere a uno dei partecipanti di farlo.

**Demonstrates**
Il proprietario del circolo apre il form di registrazione partita e seleziona 4 giocatori tra i membri, nessuno dei quali è lui stesso: la partita viene creata normalmente. Un membro qualsiasi che non è proprietario e non partecipa alla partita continua a non poter registrarla (resta bloccato come oggi).

**Acceptance Criteria**

- [ ] Il proprietario del circolo può creare una partita (`POST /circles/{circleId}/matches`) selezionando 4 giocatori che non includono lui stesso
- [ ] Un membro non-proprietario che tenta di registrare una partita cui non partecipa riceve ancora un errore (comportamento attuale invariato)
- [ ] Quando il proprietario registra una partita cui non partecipa, la conferma implicita 1/4 riservata all'inseritore NON viene applicata (lui non essendo un giocatore non può "confermare" un risultato di cui non è parte): la partita parte da 0/4 conferme
- [ ] La partita richiede comunque le conferme di tutti i 4 giocatori effettivi prima di passare a `confirmed`
- [ ] Nel frontend, il form di registrazione partita permette al proprietario di compilare le 4 posizioni giocatore senza forzare la propria selezione in una delle squadre

**Out of scope**

- Estendere il privilegio a ruoli diversi dal proprietario (es. "admin di circolo" se introdotto in futuro)
- Modificare la logica di `force-confirm` esistente (resta una funzionalità separata)
- Notifiche/email dedicate per questo caso (restano quelle già esistenti per richiesta conferma)

**Open questions**

- (nessuna)

---

#### US-026: Flusso di invito a un circolo specializzato per nuovi vs esistenti utenti

**Epic:** EP-001 | **Priority:** MEDIUM | **Story Points:** 5 | **Status:** DONE
**Approved (2026-06-23):** Review umana OK.
**Review note (2026-06-23):** Backend: nuovo `GET /circles/invite/{token}` anonimo in `src/Golp.Api/Endpoints/CircleEndpoints.cs` (valida token senza consumarlo), test in `src/Golp.Tests/Integration/JoinByTokenEndpointTests.cs` (2 nuovi, 189 totali verdi). Frontend: `circle.service.ts` (+`getInviteInfo`), `join-circle.component.ts/html` refactorati con step esplicito "Hai già usato GOLP?" (sì→login, no→registrazione, autenticato→auto-join invariato), 7 test component verdi (124 totali, 9 fail pre-esistenti non toccati: AuthService/PushNotification/AppComponent, confermati su file non modificati da questa storia). E2E `join-invite.spec.ts` estesa, 5/5 verdi. Reviewer APPROVE — no critical aperti. > **PROSSIMO PASSO:** revisione umana. Quando approvi, lancia `/eq-approve US-026` (o aggiorna manualmente lo status a `DONE`).
**Blocked by:** -

**Story**
Come giocatore invitato a unirmi a un circolo, voglio che il link di invito mi porti a una pagina dedicata che mi chiede esplicitamente se ho già usato GOLP, così che venga indirizzato direttamente al passo giusto (registrazione se sono nuovo, semplice conferma di associazione se ho già un account) senza passaggi inutili.

**Demonstrates**
Apro un link di invito a un circolo: atterro su un componente dedicato che mi chiede "Hai già usato GOLP?". Se rispondo no, vado dritto alla form di registrazione (con il circolo di destinazione già noto). Se rispondo sì, dopo il login mi viene chiesta solo la conferma di associazione al circolo (niente form di registrazione).

**Acceptance Criteria**

- [ ] Il link di invito atterra su un nuovo componente dedicato (non più direttamente su login o registrazione) che chiede esplicitamente "Hai già usato l'app GOLP?"
- [ ] Se l'utente risponde "no", viene portato alla form di registrazione esistente, con il circolo target dell'invito già associato al flusso (nessuna form di scelta del circolo)
- [ ] Se l'utente risponde "sì", viene portato al login; dopo login riuscito, vede solo una schermata di conferma associazione al circolo (no form di registrazione)
- [ ] Dopo la conferma di associazione (caso "sì") o dopo la registrazione completata (caso "no"), l'utente risulta membro del circolo invitante
- [ ] Il token/codice di invito originale resta valido durante tutto il flusso (registrazione o login), anche se l'utente naviga avanti e indietro tra le scelte
- [ ] Un token di invito non valido o già usato mostra un errore chiaro, prima ancora di chiedere "hai già usato GOLP?"

**Out of scope**

- Modificare il meccanismo di generazione/distribuzione del link di invito stesso (resta come oggi)
- Inviti multipli/bulk o gestione di inviti scaduti con rinnovo automatico
- Onboarding guidato post-registrazione oltre l'associazione al circolo (es. tour prodotto)

**Open questions**

- (nessuna)

---

> **PROSSIMO PASSO:** esegui `/eq-plan US-026` per pianificare il flusso di invito specializzato nuovo/esistente, `oppure `/eq-next` per il riepilogo dello stato corrente.
