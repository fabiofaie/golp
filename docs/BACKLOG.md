# Backlog — GOLP

**Generato il:** 2026-06-11 | **Ultima modifica:** 2026-06-11

## Riepilogo

- Epic totali: 4
- Storie totali: 11
- Storie TODO: 0 | PLANNED: 6 | IN_PROGRESS: 0 | REVIEW: 0 | DONE: 5

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

**Epic:** EP-003 | **Priority:** HIGH | **Story Points:** 5 | **Status:** IN_PROGRESS
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

**Epic:** EP-003 | **Priority:** HIGH | **Story Points:** 3 | **Status:** PLANNED
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

**Epic:** EP-003 | **Priority:** MEDIUM | **Story Points:** 2 | **Status:** PLANNED
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

## EP-004 — Premi e Statistiche

_Gamification leggera e insight personali: giocatore del mese/anno e statistiche su compagni e avversari. (da PRD §RF-5, §RF-6)_

#### US-010: Giocatore del mese e dell'anno

**Epic:** EP-004 | **Priority:** MEDIUM | **Story Points:** 3 | **Status:** PLANNED
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

**Epic:** EP-004 | **Priority:** MEDIUM | **Story Points:** 3 | **Status:** PLANNED
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

> **PROSSIMO PASSO:** scegli una user story dalla lista sopra (priorità HIGH per prima) ed esegui `/eq-plan US-001` per la pianificazione tecnica. Per un riepilogo dello stato corrente, esegui `/eq-next`.
