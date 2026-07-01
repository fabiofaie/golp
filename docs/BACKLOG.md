# Backlog — GOLP

**Generato il:** 2026-06-11 | **Ultima modifica:** 2026-06-30

## Riepilogo

- Epic totali: 6
- Storie totali: 42
- Storie TODO: 15 | PLANNED: 1 | IN_PROGRESS: 1 | REVIEW: 5 | DONE: 21

---

## EP-001 — Onboarding e Circoli

_Fondamenta multi-tenant: account giocatore, creazione circolo, appartenenza a più circoli. Senza questo nessun'altra storia è giocabile. (da PRD §RF-1)_

#### US-001: Registrazione e accesso account giocatore ✅ DONE (2026-06-11) — 3SP — EP-001

> `src/Golp.Api/` (Minimal API, EF Core, JWT, BCrypt) · `frontend/golp-app/src/app/auth/` (4 componenti, AuthService, guard, interceptor) · E2E in `frontend/golp-app/e2e/`. Test: docs/test-results/US-001/report.md

---

#### US-002: Creazione circolo con configurazione sport ✅ DONE (2026-06-11) — 3SP — EP-001

> `src/Golp.Api/Endpoints/CircleEndpoints.cs`, `Services/SportsConfig.cs`, `Data/Entities/` · `frontend/golp-app/src/app/circles/` · 12 integration test. `IsPrivate`+`JoinCode` nel modello ma non nel DTO — intenzionale.

---

#### US-003: Iscrizione a uno o più circoli ✅ DONE (2026-06-11) — 3SP — EP-001

> `CircleEndpoints.cs` (GET /circles, POST /circles/{id}/join, GET /circles/{id}/members) · `circles/browse-circles/` · 42 test totali verdi. Test: docs/test-results/US-003/report.md

---

#### US-014: Generazione e condivisione del link di invito al circolo ✅ DONE (2026-06-15) — 5SP — EP-001

> `CircleEndpoints.cs` (lazy token su `Circle.JoinCode`) · `circles/InviteDialogComponent` standalone · `circle.service.ts` · 5 integration test.

---

#### US-015: Registrazione e auto-iscrizione al circolo tramite link di invito ✅ DONE (2026-06-15) — 5SP — EP-001

> `CircleEndpoints.cs` · `circles/join-circle/` · `auth/` · `JoinByTokenEndpointTests.cs` · 148 BE + 60 FE test verdi.

---

#### US-018: Aggiunta manuale di un giocatore al circolo da parte del proprietario ✅ DONE (2026-06-23) — 5SP — EP-001

> `CircleEndpoints.cs` (POST /circles/{id}/members, owner-only) · `circles/add-member-dialog/` · `IEmailService`/`DevelopmentEmailService.cs` · 9 integration test + 7 unit Angular + 2 E2E.

---

#### US-019: Sessione lunga tramite refresh token ✅ DONE (2026-06-23) — 5SP — EP-001

> `RefreshToken` entity + migration · `RefreshTokenService.cs` · `AuthEndpoints.cs` (+/auth/refresh, /auth/logout) · `auth.interceptor.ts` (retry automatico su 401) · 10 integration test.

---

#### US-024: Guida all'installazione della PWA per nuovi utenti da browser ✅ DONE (2026-06-23) — 5SP — EP-001

> `shared/pwa-install/` (banner, guide, steps, service, platform-service, ognuno con spec) · E2E in `e2e/pwa-install.spec.ts`.

---

#### US-026: Flusso di invito specializzato per nuovi vs esistenti utenti ✅ DONE (2026-06-23) — 5SP — EP-001

> `GET /circles/invite/{token}` anonimo · `join-circle.component.ts/html` con step esplicito "Hai già usato GOLP?" · 5/5 E2E verdi.

---

#### US-038: Notifica email allo staff per nuove registrazioni

**Epic:** EP-001 | **Priority:** MEDIUM | **Story Points:** 3 | **Status:** DONE
**Blocked by:** -
**Review note (2026-06-29):** Codice in `src/Golp.Api/` (IEmailService, SmtpEmailService, DevelopmentEmailService, AuthEndpoints, CircleEndpoints, 2 template HTML), test in `src/Golp.Tests/Integration/` (4 nuovi test, 243/243 verde). Reviewer APPROVE.
**Approved (2026-06-29):** Review umana OK.

**Story**
Come gestore del progetto GOLP, voglio ricevere una email a `iscrizioni.golp@eqproject.it` ogni volta che un nuovo utente si registra o un nuovo circolo viene creato, così che possa monitorare la crescita della piattaforma senza dover controllare il database.

**Demonstrates**
Registrando un nuovo account giocatore (US-001) arriva una email a `iscrizioni.golp@eqproject.it` con i dati essenziali del nuovo utente. Creando un nuovo circolo (US-002) arriva una email separata con i dati essenziali del nuovo circolo.

**Acceptance Criteria**

- [ ] Al completamento con successo della registrazione di un nuovo utente (`POST /auth/register`), viene inviata una email a `iscrizioni.golp@eqproject.it` con almeno: email e nome utente registrato, data/ora
- [ ] Al completamento con successo della creazione di un nuovo circolo (`POST /circles`), viene inviata una email a `iscrizioni.golp@eqproject.it` con almeno: nome circolo, sport configurato, utente creatore, data/ora
- [ ] Un fallimento nell'invio dell'email non blocca né fa fallire la registrazione/creazione circolo (l'operazione principale resta sincrona e indipendente dalla notifica)
- [ ] In ambiente di sviluppo l'invio reale è disattivabile/sostituibile da un log console (coerente con `DevelopmentEmailService` esistente), per non spammare la casella reale durante i test

**Out of scope**

- Email di benvenuto/conferma indirizzata al nuovo utente stesso (resta distinta da questa notifica interna)
- Digest aggregato (es. riepilogo giornaliero) — questa storia è notifica immediata per singolo evento
- Configurazione UI per disattivare la notifica — è un comportamento di sistema, non una preferenza utente

**Open questions**

- Provider SMTP/transazionale di produzione da usare (es. SMTP relay esistente, SendGrid, ecc.) — da chiarire in `/eq-plan`

---

## EP-002 — Partite e Validazione

_Il cuore del dato oggettivo: inserire una partita 2v2 e farla confermare da tutti e 4 i giocatori. Senza conferma 4/4 il ranking perde credibilità. (da PRD §RF-2, §RF-3)_

#### US-004: Inserimento risultato partita 2v2 ✅ DONE (2026-06-11) — 5SP — EP-002

> `MatchEndpoints.cs`, `Data/Entities/Match.cs|MatchSet.cs`, migration `AddMatchesAndMatchSets` · `circles/record-match/` · 13 integration test.

---

#### US-005: Conferma collettiva del risultato (4/4) ✅ DONE (2026-06-12) — 5SP — EP-002

> `GET /circles/{circleId}/matches/{matchId}` · `MatchConfirmComponent` (score hero, progress dots, CTA, feedback animato) · `ConfirmMatchAsync` con transizione a `confirmed`.

---

#### US-013: Conferma forzata del risultato da parte del proprietario del circolo ✅ DONE (2026-06-15) — 3SP — EP-002

> `ForceConfirmMatchAsync` (`POST .../force-confirm`) · `Match.ForceConfirmedById/At` audit · `isOwner` in `CircleMatchHistoryComponent` · 8 integration test.

---

#### US-025: Il proprietario del circolo può registrare partite cui non partecipa ✅ DONE (2026-06-22) — 3SP — EP-002

> `MatchEndpoints.CreateMatchAsync`: eccezione owner + conferma implicita condizionale · 2 nuovi integration test. Deviazione: errore resta `400` (non `403`) per compatibilità test.

---

#### US-036: Conferma esplicita di irreversibilità prima della forzatura ✅ DONE (2026-06-26) — 2SP — EP-002

> Stato inline a due step in `circle-match-history.component.ts/html` · test unit + E2E `circle-match-history.spec.ts`.

---

#### US-037: Pagina di dettaglio partita ✅ DONE (2026-06-25) — 5SP — EP-002

> `MatchEndpoints.cs` (membership check) · `circles/match-detail/` · route in `app.routes.ts` · mockup in `docs/mockups/US-037/` · 6 integration + 6 unit + 3 E2E.

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

#### US-020: Email su template riutilizzabili + notifiche email conferma e contestazione partita

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

## EP-003 — Ranking e Classifica

_Il valore core: trasformare partite confermate in una classifica oggettiva, aggiornata in tempo reale. (da PRD §RF-4, §Algoritmo di ranking)_

#### US-007: Calcolo rating ELO alla conferma partita ✅ DONE (2026-06-12) — 5SP — EP-003

> `RatingService` (formula PRD §Algoritmo: amplifier 0.7, K=32/48, clamp score_ratio [0.5,1.0]) · 4 campi delta `int?` su `Match` + migration · 9 unit + 3 integration test.

---

#### US-008: Classifica circolo in tempo reale ✅ DONE (2026-06-12) — 3SP — EP-003

> `GET /circles/{id}/leaderboard` (classified/unclassified split, rating DESC + confirmedMatches DESC) · `CircleLeaderboardComponent` (podio top-3, highlight utente) · 7 integration + 4 E2E.

---

#### US-009: Esito partita con delta punti (+N / −N) ✅ DONE (2026-06-12) — 2SP — EP-003

> `GetMatchesAsync` calcola `myDelta` dalle 4 posizioni player · badge `+N pt`/`−N pt` in `circle-match-history.component.html` con CSS variables · 7 integration + 3 E2E.

---

#### US-012: Rating ELO pesato su set per sport a set ✅ DONE (2026-06-15) — 3SP — EP-003

> `SportsConfig.SportDto`: `SetWeight=0.4` per padel/beachtennis · `RatingService`: branch blended `α×set_ratio + (1-α)×game_ratio` · 13 unit + 4 integration = 130 test verdi.

---

#### US-017: Pagina spiegazione algoritmo ELO e simulatore partita ✅ DONE (2026-06-23) — 5SP — EP-003

> `RatingService.ComputeDeltas` (public static) · `SimulateEndpoints.cs` (`POST /simulate-match` pubblico) · `elo-info/` (service + component + route) · 10 integration test.

---

#### US-034: Margine corretto per partite pari su set o game ✅ DONE (2026-06-24) — 5SP — EP-003

> `MatchEndpoints.cs` (pareggio set ammesso se game decidono) · `RatingService.cs` (scoreRatio: peso set/game forzato a 0 se pari; floor ±1) · 3 integration + 3 unit. Suite 193/193 verde.

---

#### US-035: Notifica variazione posizione in classifica

**Epic:** EP-003 | **Priority:** MEDIUM | **Story Points:** 3 | **Status:** DONE
**Approved (2026-06-29):** Review umana OK.
**Review note (2026-06-29):** `IRatingService.CalculateAndApplyAsync` restituisce `IReadOnlyList<(Guid UserId, int NewPosition)>` giocatori saliti di posizione. `RatingService`: snapshot pre-update (query DB tutti i membri del circolo) + post-update (stesso snapshot con rating modificati in memoria) → confronto posizione 1-based. `PushNotificationService.SendRankingImprovedAsync`: fire-and-forget, pattern dead-token identico a `SendConfirmationRequestAsync`. `MatchEndpoints.ConfirmMatchAsync`: dopo `SaveChanges`, per ogni migliorato lancia push con nome circolo. Test: 4 nuovi integration `RatingServiceIntegrationTests` (pre/post ranking, losers, multiple, position reflect) + 4 unit `PushNotificationServiceTests` (no token, payload, dead token, no-propagate) + 1 integration E2E `MatchIntegrationTests.ConfirmFourth_PlayerRises_ReceivesRankingPush`. Suite: 239/239 verde. Reviewer APPROVE. > **PROSSIMO PASSO:** revisione umana. Quando approvi: `/eq-approve US-035`.
**Blocked by:** US-029

**Story**
Come giocatore, voglio essere notificato quando la mia posizione in classifica nel circolo cambia per effetto di una variazione diretta del mio rating, così che possa accorgermi senza dover controllare manualmente la classifica.

**Demonstrates**
Quando una partita `confirmed` aggiorna il rating di un giocatore e questo aggiornamento gli fa salire di posizione nella classifica del circolo, il giocatore riceve una notifica push (se attiva, vedi US-029) con il dettaglio della nuova posizione. Chi scende di posizione o non cambia posizione non riceve notifica.

**Acceptance Criteria**

- [ ] Dopo la conferma di una partita (4/4), per ciascun giocatore coinvolto viene confrontata la posizione in classifica del circolo prima e dopo l'aggiornamento del rating
- [ ] Se la posizione migliora (sale), il giocatore riceve una notifica con la nuova posizione (es. "Sei salito al 3° posto nel circolo X")
- [ ] Se la posizione non cambia o peggiora (scende), nessuna notifica viene generata per quel giocatore
- [ ] La notifica viene inviata solo ai giocatori che hanno le notifiche push attive (dipendenza da US-029)
- [ ] Il calcolo della posizione si basa sul ranking per `Rating` all'interno dello stesso circolo (`CircleMembership.Rating`), non su classifiche globali
- [ ] Se più giocatori della stessa partita salgono di posizione, ciascuno riceve la propria notifica indipendente (nessun raggruppamento)

**Out of scope**

- Notifica per chi scende di posizione (deciso: solo salite generano notifica)
- Notifica per variazioni di rating che non derivano da una partita confermata (es. correzioni manuali admin)
- Notifica aggregata periodica ("riepilogo settimanale") — solo evento puntuale per partita
- Canali diversi dalla push notification (email, SMS)

**Open questions**

- (nessuna — confermato: notifica solo a chi sale di posizione)

---

## EP-004 — Premi e Statistiche

_Gamification leggera e insight personali: giocatore del mese/anno e statistiche su compagni e avversari. (da PRD §RF-5, §RF-6)_

#### US-010: Giocatore del mese e dell'anno ✅ DONE (2026-06-12) — 3SP — EP-004

> `CircleAward` entity + migration `AddCircleAwards` · `AwardsEndpoints.cs` (GET /circles/{id}/awards, on-the-fly, tie-break deterministico) · `CircleAwardsComponent` · 8 integration + 3 E2E.

---

#### US-011: Statistiche personali — compagni e avversari ✅ DONE (2026-06-13) — 3SP — EP-004

> `StatsEndpoints.cs` (GET /circles/{circleId}/stats/me, soglia N=3) · `CircleStatsComponent` (ring SVG) · fix `proxy.conf.js` per E2E · 9 integration + 3 E2E.

---

#### US-021: Notifica email automatica giocatore del mese/anno

**Epic:** EP-004 | **Priority:** LOW | **Story Points:** 5 | **Status:** DONE
**Approved (2026-06-29):** Review umana OK.
**Review note (2026-06-29):** Codice in `src/Golp.Api/Data/Entities/AwardNotificationSent.cs` (entità idempotenza), `Services/{IAwardsCalculator,AwardsCalculator}.cs` (logica periodo estratta da AwardsEndpoints), `Services/{IEmailService,SmtpEmailService,DevelopmentEmailService}.cs` (nuovo `SendAwardWinnerEmailAsync`), `EmailTemplates/award-winner.html`, `Services/{IAwardNotificationProcessor,AwardNotificationProcessor,AwardNotificationBackgroundService}.cs`, `Program.cs` (+HostedService), `appsettings.json` (+Awards:NotificationCheckHourUtc). Migration `AddAwardNotificationsSent`. Test: 4 integration in `AwardNotificationProcessorTests.cs` + 1 unit in `EmailTemplateRendererTests.cs` + 8 AwardsEndpointTests verdi dopo refactor. Suite completa: 230/230 verde. Reviewer APPROVE. > **PROSSIMO PASSO:** revisione umana. Quando approvi: `/eq-approve US-021`.
**Blocked by:** US-020

**Story**
Come giocatore che vince il premio "giocatore del mese" o "giocatore dell'anno" in un circolo, voglio ricevere una email automatica che me lo comunica, così che non debba scoprirlo per caso aprendo la pagina premi.

**Demonstrates**
Un job schedulato calcola, alla chiusura di ogni mese/anno, il vincitore di ciascun circolo (stessa logica oggi esposta on-demand da `GET /circles/{id}/awards`) e invia una email al vincitore con il periodo e il risultato. Il job gira una sola volta per periodo per circolo, anche se eseguito più volte (idempotente).

**Acceptance Criteria**

- [ ] Esiste un `BackgroundService` registrato in `Program.cs` di `src/Golp.Api` (stesso processo dell'API, nessun sito/processo Azure aggiuntivo) che gira periodicamente (es. ogni notte) e verifica se è il primo giorno di un nuovo mese/anno per cui calcolare il vincitore del periodo precedente
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

- (nessuna — job in-process via `BackgroundService` in `Golp.Api`, stesso App Service esistente)

---

## EP-005 — Configurazione e Amministrazione

_Funzionalità per gestire la piattaforma senza dover rilasciare nuove versioni: sport, parametri ELO, configurazioni operative._

#### US-022: Numero di versione visibile in login e dashboard ✅ DONE (2026-06-22) — 3SP — EP-005

> `scripts/generate-version.js` · `shared/version/app-version.component.ts` · wiring in `scripts/deploy-frontend.ps1` · 2 unit test.

---

#### US-023: Aggiornamento automatico dei client dopo un rilascio ✅ DONE (2026-06-22) — 5SP — EP-005

> `shared/update/AppUpdateService` + `AppUpdateBannerComponent` · `app.component.ts` (visibilitychange + NavigationEnd) · cache hardening in `public/web.config` · 12 unit test.

---

#### US-027: Palette colori tema chiaro con contrasto verificato ✅ DONE (2026-06-23) — 5SP — EP-005

> `styles.scss` (blocco `:root.theme-light`, 65 `--color-*` parallele al tema scuro) · mockup in `docs/mockups/US-027/` · blocco inerte: attivazione = US-028.

---

#### US-028: Switch manuale tema chiaro/scuro con persistenza per device ✅ DONE (2026-06-23) — 5SP — EP-005

> `theme/theme.service.ts` (signal + effect, persistenza `localStorage`) · `profile/profile.component.ts` (toggle) · rotta `/profilo` · 5 unit + 3 component + 2 E2E. Test: docs/test-results/US-028/report.md

---

#### US-029: Attivazione/disattivazione notifiche push dalla pagina Profilo ✅ DONE (2026-06-25) — 3SP — EP-005

> `PushEndpoints.cs` (`POST /api/push/test`) · `profile.component.ts` (toggle on/off, test-send, guida PWA condizionale riusando `PwaInstallGuideComponent`) · 5 nuovi BE test + 2 E2E.

---

#### US-030: Modifica nome visualizzato dalla pagina Profilo ✅ DONE (2026-06-26) — 3SP — EP-005

> `AuthEndpoints.cs` · `auth/current-user.service.ts` · `profile/profile.component.ts` · 6 integration + 4 unit service + 5 component + 2 E2E.

---

#### US-031: Logout da tutti i device dal Profilo ✅ DONE (2026-06-25) — 5SP — EP-005

> `User.SecurityStamp` (Guid) + claim JWT + validazione `OnTokenValidated` · `POST /auth/logout-all` · conferma inline a due step · 5 nuovi unit/integration + 1 E2E.

---

#### US-032: Eliminazione account dal Profilo ✅ DONE (2026-06-25) — 8SP — EP-005

> `POST /auth/me/delete` (anonimizza User, rimuove CircleMembership, annulla Match pending) · fix `auth.interceptor.ts` (401 su delete non triggerava refresh) · 6 integration `AccountDeletionIntegrationTests` + 1 E2E.

---

#### US-033: Riepilogo rating per circolo nel Profilo

**Epic:** EP-005 | **Priority:** LOW | **Story Points:** 2 | **Status:** DONE
**Approved (2026-06-29):** Review umana OK.
**Review note (2026-06-29):** Codice in `frontend/golp-app/src/app/profile/profile.component.ts` (sezione "I tuoi circoli" con `circleService.getMyCircles()`, signal `myCircles`/`circlesLoading`, metodo `goToCircle()`). Test unit in `profile.component.spec.ts` (3 nuovi, 29/29 verdi), e2e in `e2e/profile-circles-summary.spec.ts` (2/2 verdi). Suite completa: 185/185 unit. Reviewer APPROVE — no Critical aperti. > **PROSSIMO PASSO:** revisione umana. Quando approvi, lancia `/eq-approve US-033`.
**Blocked by:** US-028

**Story**
Come utente dell'app, voglio vedere nel mio Profilo il rating attuale in ciascun circolo a cui appartengo, così che abbia una vista d'insieme senza dover entrare in ogni circolo singolarmente.

**Demonstrates**
La pagina Profilo mostra un elenco dei circoli dell'utente con, per ciascuno, il rating attuale (dato già esposto da `GET /circles/me`) e il nome del circolo. Cliccando un circolo si naviga alla sua vista dedicata.

**Acceptance Criteria**

- [ ] Il Profilo mostra un elenco con nome circolo + rating attuale per ogni circolo di cui l'utente è membro
- [ ] L'elenco usa i dati già disponibili da `GET /circles/me` (nessun nuovo endpoint backend necessario)
- [ ] Cliccando una riga dell'elenco, l'utente viene portato alla pagina del circolo corrispondente
- [ ] Se l'utente non è membro di nessun circolo, viene mostrato un messaggio chiaro invece di una lista vuota muta

**Out of scope**

- Grafici storici di andamento rating (resta nella sezione statistiche esistente, se presente)
- Confronto tra circoli o classifiche aggregate

**Open questions**

- (nessuna)

---

#### US-016: Sport configurabili da database

**Epic:** EP-005 | **Priority:** HIGH | **Story Points:** 5 | **Status:** DONE
**Approved (2026-06-29):** Review umana OK.
**Review note (2026-06-24):** Codice in `src/Golp.Api/{Data/Entities/Sport.cs, Data/AppDbContext.cs, Services/{ISportsService,SportsService}.cs, Endpoints/CircleEndpoints.cs, Services/RatingService.cs}`, migration `20260624145528_AddSportsTable`, test in `src/Golp.Tests/{Services/SportsServiceTests.cs, Integration/CircleIntegrationTests.cs, +17 test factory fixes}`. Reviewer APPROVE.
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
- [ ] La colonna `Key` è solo display/lookup: il riferimento allo sport nei circoli/match resta come oggi (nessuna modifica al modo in cui `Circle` referenzia lo sport), `Key` serve solo a mappare la riga DB ai valori `SportsConfig` esistenti durante la migration

**Out of scope**

- UI di amministrazione per gestire sport (CRUD via interfaccia grafica)
- Autenticazione/autorizzazione per la modifica della tabella Sports (gestita solo via accesso diretto al DB)
- Parametri ELO (K, amplifier) in database

**Open questions**

- (nessuna — `Key` confermata come solo display/lookup, non FK)

---

## EP-006 — Growth e Viralità

_Abbattere le barriere all'ingresso per far crescere la base utenti: ospiti nelle partite, link di conferma pubblici, quick match e notifiche WhatsApp. Ogni partita registrata è un'opportunità di acquisizione._

---

#### US-039: Giocatore ospite in una partita

**Epic:** EP-006 | **Priority:** HIGH | **Story Points:** 8 | **Status:** REVIEW
**Blocked by:** -
**Review note (2026-06-30):** Codice in `src/Golp.Api/Endpoints/MatchEndpoints.cs`, `CircleEndpoints.cs`, `Data/Entities/User.cs`, `Data/AppDbContext.cs`, `Migrations/`. FE in `frontend/golp-app/src/app/circles/record-match/`, `circle-match-history/`, `circle-leaderboard/`, `match.service.ts`, `circle.service.ts`, `styles.scss`. Test in `src/Golp.Tests/Integration/MatchIntegrationTests.cs` (5 nuovi test guest). E2E in `e2e/record-match-guest.spec.ts`. Reviewer: APPROVE. > **PROSSIMO PASSO:** revisione umana. Quando approvi, lancia `/eq-approve US-039`.

**Story**
Come giocatore registrato, voglio aggiungere un ospite (nome + email o telefono) a uno slot di una partita invece di dover selezionare solo membri registrati del circolo, così che posso registrare partite con chiunque anche se non è ancora su GOLP.

**Demonstrates**
Nella pagina "Registra Partita", per ogni slot giocatore posso scegliere tra "Membro del circolo" (dropdown esistente) e "Ospite" (toggle → campi nome + email/telefono). Confermando la partita, il sistema cerca l'email/telefono tra gli utenti esistenti: se trovato usa quell'account, se non trovato crea un `User` con `IsActivated = false` e lo aggiunge come membro del circolo con rating 1000. La partita viene registrata e ranked normalmente. L'ospite riceve una notifica (email o push) con il link di conferma.

**Acceptance Criteria**

- [ ] Ogni slot giocatore nella pagina "Registra Partita" ha un toggle [Membro / Ospite]
- [ ] In modalità Ospite lo slot mostra: campo Nome (obbligatorio) + campo Email o Telefono (obbligatorio)
- [ ] Se `Contact Picker API` è disponibile (mobile), lo slot ospite mostra pulsante "Scegli dai contatti" che precompila Nome e Telefono tramite `navigator.contacts.select(['name', 'tel'])`
- [ ] Se `Contact Picker API` non è disponibile (desktop o browser non supportato), i campi restano input manuali senza degradare l'esperienza
- [ ] Se l'email/telefono corrisponde a un User esistente, viene usato quell'account (nessun duplicato)
- [ ] Se l'email/telefono non esiste, viene creato un `User` con `IsActivated = false` e `PasswordHash = null`
- [ ] L'ospite creato riceve automaticamente una `CircleMembership` nel circolo con `Rating = 1000`
- [ ] La partita viene creata e ranked come una partita normale (ospite trattato come rating 1000)
- [ ] L'ospite riceve notifica email con link alla pagina di conferma (vedi US-040)
- [ ] Un utente con `IsActivated = false` può fare "Recupera password" con la sua email per attivare l'account
- [ ] In classifica e storico partite, gli ospiti non ancora attivati mostrano il nome con indicatore "(non registrato)"

**Out of scope**

- Notifica via WhatsApp (US-042)
- Pagina pubblica di conferma (US-040, dipendenza di questa storia)
- Ospiti senza email e senza telefono (almeno un contatto è obbligatorio per il growth loop)
- UI admin per gestire utenti ghost

**Open questions**

- (nessuna)

---

#### US-040: Pagina pubblica conferma partita via token temporaneo

**Epic:** EP-006 | **Priority:** HIGH | **Story Points:** 5 | **Status:** DONE
**Blocked by:** US-039
**Review note (2026-07-01):** Codice in `src/Golp.Api/Endpoints/PublicMatchEndpoints.cs`, entity `MatchConfirmationToken`, frontend `src/app/public/match-public-confirm/`. Test in `PublicMatchTokenTests.cs`. 257/257 backend tests verdi. Reviewer: APPROVE.
**Approved (2026-07-01):** Review umana OK.

**Story**
Come giocatore (registrato o ospite), voglio aprire un link ricevuto via email o notifica e vedere una pagina riepilogativa della partita accessibile senza login, così che posso confermare o contestare la partita con un solo tap.

**Demonstrates**
Il link `golp.app/m/{token}` apre una pagina pubblica (no auth required) che mostra: sport, nome del circolo, squadre con nomi giocatori, risultato, e chi ha già confermato. Due soli pulsanti: "Conferma" e "Contesta". Cliccando uno dei due, il token viene consumato e lo stato di conferma dell'utente aggiornato. La pagina mostra un messaggio di successo e invita l'ospite non registrato a creare un account.

**Acceptance Criteria**

- [ ] Esiste una tabella `MatchConfirmationTokens` con: `Id`, `MatchId`, `UserId`, `Token` (UUID), `ExpiresAt` (72h dalla creazione), `UsedAt` (nullable)
- [ ] I token vengono generati al momento della creazione della partita (uno per ogni giocatore non-creatore)
- [ ] `GET /m/{token}` è un endpoint pubblico (no JWT) che restituisce riepilogo partita + stato token
- [ ] La pagina mostra: sport, circolo, squadre, risultato, lista "chi ha confermato" con spunta per ognuno
- [ ] I pulsanti Conferma e Disputa chiamano `POST /m/{token}/confirm` e `POST /m/{token}/dispute`
- [ ] Token scaduto o già usato mostra messaggio chiaro e link per accedere all'app
- [ ] Dopo conferma/disputa, se l'utente è `IsActivated = false`, la pagina mostra CTA "Crea il tuo account GOLP"
- [ ] Il flusso di conferma via token è equivalente al flusso di conferma autenticato esistente (stesso effetto su `Match.Status` e rating)

**Out of scope**

- Login dalla pagina pubblica
- Modifica del risultato dalla pagina pubblica
- Pagina pubblica del profilo giocatore

**Open questions**

- (nessuna)

---

#### US-041: Quick Match — registra partita e crea circolo in un'unica azione

**Epic:** EP-006 | **Priority:** HIGH | **Story Points:** 8 | **Status:** TODO
**Blocked by:** US-039, US-040

**Story**
Come giocatore registrato, voglio registrare una partita con amici senza dover prima creare un circolo separatamente, così che posso iniziare a usare GOLP immediatamente per qualsiasi gruppo di gioco senza setup preliminare.

**Demonstrates**
Dalla dashboard, il pulsante principale "Registra Partita" avvia un flusso unificato: scelgo lo sport, inserisco un nome opzionale per il circolo (altrimenti auto-generato come "Padel con Mario e Anna"), aggiungo i 3 altri giocatori (registrati o ospiti con nome+contatto), inserisco il risultato e invio. Il backend crea il circolo (privato), aggiunge tutti come membri, crea eventuali utenti ghost e registra la partita in un'unica transazione. Gli ospiti ricevono la notifica con il link di conferma.

**Acceptance Criteria**

- [ ] Dalla dashboard esiste un pulsante primario "Registra Partita" visibile senza dover entrare in un circolo
- [ ] Il flusso chiede: sport → nome circolo (opzionale, con placeholder auto-generato) → 3 giocatori (membro esistente o ospite) → risultato
- [ ] Il backend gestisce la creazione atomica: `Circle` (privato, `IsPrivate = true`) + `CircleMembership` per tutti + eventuali `User` ghost + `Match`
- [ ] Se uno dei giocatori è già membro di un circolo con lo stesso sport e gli stessi partecipanti, viene proposto di usare il circolo esistente invece di crearne uno nuovo
- [ ] Il circolo creato è privato per default (`IsPrivate = true`): non appare in "Sfoglia Circoli"
- [ ] Il creatore viene aggiunto automaticamente come membro del nuovo circolo
- [ ] Tutti i giocatori ricevono notifica (push o email) al termine del flusso
- [ ] Il flusso "Registra Partita" da dentro un circolo esistente rimane invariato

**Out of scope**

- Impostazioni avanzate del circolo durante il quick match (join code, visibilità pubblica)
- Rilevamento duplicati per circoli con più di 2 partecipanti in comune (solo exact match 4/4)

**Open questions**

- (nessuna)

---

#### US-042: Condivisione link conferma partita via WhatsApp

**Epic:** EP-006 | **Priority:** MEDIUM | **Story Points:** 3 | **Status:** TODO
**Blocked by:** US-039

**Story**
Come registratore di una partita, voglio poter inviare il link di conferma ai giocatori tramite WhatsApp con un solo tap, così che lo ricevono immediatamente e hanno più probabilità di confermare in tempo.

**Demonstrates**
Dopo aver registrato una partita, l'utente vede la pagina di successo con un pulsante "Invia su WhatsApp" per ogni giocatore/ospite che ha un numero di telefono. Cliccando il pulsante, si apre WhatsApp (app o web) con il numero precompilato e il messaggio: *"Ciao Marco! Ho registrato una partita di Padel nel circolo Tennis Club. Conferma il risultato qui: golp.app/m/abc123"*. Il giocatore riceve il messaggio e clicca il link per confermare.

**Acceptance Criteria**

- [ ] Dopo la creazione di una partita, la pagina di successo mostra un pulsante "Invia su WhatsApp" per ogni partecipante con numero di telefono
- [ ] Il pulsante apre `https://wa.me/<phone>?text=<messaggio_encoded>` (su mobile apre l'app WhatsApp, su desktop apre WhatsApp Web)
- [ ] Il numero di telefono è normalizzato al formato internazionale senza `+` (es. `393401234567` per un numero italiano)
- [ ] Il messaggio precompilato contiene: nome del partecipante, sport, nome del circolo, link conferma con token (es. `golp.app/m/<token>`)
- [ ] Se un partecipante non ha numero di telefono, il pulsante WhatsApp non appare (solo email)
- [ ] Il link funziona senza che il registratore abbia salvato il numero in rubrica

**Out of scope**

- Invio automatico server-side via WhatsApp Business API
- Notifiche WhatsApp per eventi diversi dalla conferma partita (ranking, awards)
- Chatbot WhatsApp bidirezionale
- Verifica numero di telefono con OTP

**Open questions**

- (nessuna)

---

> **PROSSIMO PASSO:** avvia il piano tecnico della prima storia del growth loop con `/eq-plan US-039`.

---
