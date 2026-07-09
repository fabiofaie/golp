# Backlog — GOLP

**Generato il:** 2026-06-11 | **Ultima modifica:** 2026-07-09

## Riepilogo

- Epic totali: 10
- Storie totali: 60
- Storie TODO: 8 | PLANNED: 0 | IN_PROGRESS: 0 | REVIEW: 6 | DONE: 46

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

#### US-027: Palette colori tema chiaro con contrasto verificato

**Epic:** EP-005 | **Priority:** MEDIUM | **Story Points:** 5 | **Status:** DONE
**Approved (2026-06-23):** Review umana OK. Token validati live dal tema chiaro funzionante di US-028.
**Review note (2026-06-23):** Codice in `frontend/golp-app/src/styles.scss` (nuovo blocco `:root.theme-light` con tutte le 65 `--color-*` ridefinite, parallele a quelle scure — verificato 1:1 via grep, nessuna var orfana). Token derivati da `docs/mockups/US-027/style-tokens.json` (mockup comparativo scuro/chiaro in `docs/mockups/US-027/index.html`). Blocco inerte: nessun componente applica la classe `theme-light` in questa storia (attivazione = US-028). Test suite frontend: 115/124 verdi, 9 fail pre-esistenti non toccati (PushNotificationService/AuthService/AppComponent). Reviewer APPROVE — no critical aperti. Nota non bloccante: alcuni valori hardcoded fuori da `:root` (`.score-input`, breakpoint desktop `body`, `.btn-action-primary`, `.feedback-icon`) restano scuri e andranno gestiti quando US-028 attiva il toggle. > **PROSSIMO PASSO:** revisione umana. Quando approvi, lancia `/eq-approve US-027` (o aggiorna manualmente lo status a `DONE`).
**Blocked by:** -

**Story**
Come utente dell'app, voglio una palette colori chiara alternativa a quella scura attuale, così che possa scegliere il tema più leggibile per me senza perdere distinzione visiva tra elementi (oro/argento/bronzo, partner/avversario, sport, stati di errore/successo).

**Demonstrates**
Esiste un secondo set di token CSS (tema chiaro) accanto a quello scuro esistente in `styles.scss`: sfondo, superfici, testo, border invertiti su base chiara; i colori semantici (accent, oro/argento/bronzo, partner/avversario, sport, errore/successo) sono ricalibrati per garantire contrasto leggibile (AA) su sfondo chiaro, pur restando riconoscibili come "lo stesso colore" del tema scuro.

**Acceptance Criteria**

- [ ] Esiste un set completo di token colore per il tema chiaro, parallelo a quello scuro esistente (stessa lista di variabili `--color-*`)
- [ ] Testo primario/secondario su sfondo chiaro rispetta un contrasto minimo WCAG AA (4.5:1 per testo normale, 3:1 per testo grande)
- [ ] I colori oro/argento/bronzo (classifica) restano distinguibili tra loro su sfondo chiaro
- [ ] I colori partner/avversario (statistiche) restano distinguibili tra loro su sfondo chiaro
- [ ] I colori per sport (padel/beach tennis/basket/burraco) restano distinguibili tra loro su sfondo chiaro
- [ ] I colori di errore/successo restano riconoscibili (non confondibili con altri stati) su sfondo chiaro
- [ ] Nessuna pagina esistente applica ancora il tema chiaro in questa storia (solo i token sono definiti, l'attivazione è in US-028)

**Out of scope**

- Attivazione/switch del tema (vedi US-028)
- Modifiche al layout o alla struttura dei componenti, solo colori

**Open questions**

- (nessuna)

---

#### US-028: Switch manuale tema chiaro/scuro con persistenza per device

**Epic:** EP-005 | **Priority:** MEDIUM | **Story Points:** 5 | **Status:** DONE
**Approved (2026-06-23):** Review umana OK.
**Review note (2026-06-23):** Nuovo `frontend/golp-app/src/app/theme/theme.service.ts` (signal + effect che applica classe `theme-light` su `document.documentElement`, default scuro, persistenza `localStorage` chiave `golp_theme`), inject in `app.component.ts` per attivazione al bootstrap. Nuovo `profile/profile.component.ts` (pagina Profilo con toggle Scuro/Chiaro, classi esistenti), rotta `/profilo` protetta in `app.routes.ts`, link in `dashboard.component.ts`. Test: 5 unit (ThemeService) + 3 component (ProfileComponent) + 2 e2e (`e2e/profile-theme.spec.ts`: default scuro, toggle chiaro, persistenza post-reload, cross-pagina) tutti verdi. Suite: 123/132 verdi, 9 fail pre-esistenti non toccati (PushNotificationService/AuthService/AppComponent `should render title` cerca boilerplate Angular rimosso). Reviewer APPROVE — no critical. Nota non bloccante: valori hardcoded fuori `:root` (`.score-input`, breakpoint desktop `body`, `.feedback-icon`) restano scuri anche in tema chiaro (rischio già noto da US-027) — follow-up per renderli tema-aware. > **PROSSIMO PASSO:** revisione umana. Quando approvi, lancia `/eq-approve US-028` (o aggiorna manualmente lo status a `DONE`).
**Visual evidence (2026-06-23):** docs/test-results/US-028/report.md — verdetto **APPROVE** dopo fix. Il difetto iniziale (tema chiaro illeggibile su desktop ≥600px per valori CSS hardcoded fuori da `:root`) è stato corretto in reopen: nuovi token `--color-bg-deep`/`--color-input-bg`/`--color-input-border` in `styles.scss` + `style-tokens.json`, `.page{background:var(--color-bg)}`, media query desktop e score-input ora tema-aware. Re-verifica: screenshot `AC-3-FIXED-*` mostrano Profilo e dashboard leggibili in chiaro su desktop, e2e 2/2, suite 123/132 (9 pre-esistenti).
**Blocked by:** US-027

**Story**
Come utente dell'app, voglio poter scegliere tra tema scuro e tema chiaro da una pagina impostazioni, così che la mia scelta resti applicata ad ogni mia visita successiva su questo dispositivo.

**Demonstrates**
Una nuova pagina "Profilo" (raggiungibile dall'area autenticata) mostra un controllo per scegliere tema scuro/chiaro. L'app parte sempre in tema scuro di default per un utente che non ha mai scelto; cambiando il controllo, l'interfaccia si aggiorna immediatamente con i token di US-027; la scelta resta valida nelle visite successive sullo stesso browser/device, anche dopo logout/login.

**Acceptance Criteria**

- [ ] Esiste una pagina/sezione "Profilo" raggiungibile dall'area autenticata con un controllo tema scuro/chiaro
- [ ] Senza una scelta precedente salvata, l'app applica il tema scuro di default
- [ ] Cambiando il controllo, l'interfaccia applica immediatamente la palette corrispondente (tutte le pagine, non solo quella impostazioni) senza reload manuale
- [ ] La scelta di tema viene salvata in `localStorage` (non sul backend, non legata all'account)
- [ ] Ricaricando la pagina o tornando sull'app in una sessione successiva sullo stesso device, il tema scelto resta applicato
- [ ] Su un device/browser diverso (o dopo pulizia localStorage), l'app torna al default scuro

**Out of scope**

- Sincronizzazione della preferenza tra device diversi o legata all'account (richiederebbe backend, non in questa storia)
- Tema automatico basato su `prefers-color-scheme` del sistema operativo
- Pagina impostazioni con altre opzioni oltre al tema (resta minimale, solo il toggle tema in questa storia)

**Open questions**

- (nessuna)

---

#### US-029: Attivazione/disattivazione notifiche push dalla pagina Profilo

**Epic:** EP-005 | **Priority:** MEDIUM | **Story Points:** 3 | **Status:** DONE
**Approved (2026-06-25):** Review umana OK.
**Review note (2026-06-24):** Backend: `POST /api/push/test` autenticato in `PushEndpoints.cs` + `SendTestNotificationAsync` in `PushNotificationService.cs` (riusa `FcmTokens`/`IFcmSender` esistenti, isolamento per `userId` da claim JWT). Frontend: `profile.component.ts` con sezione "Notifiche push" (toggle on/off, test-send, guida installazione condizionale riusando `PwaInstallGuideComponent` di US-024). Test: backend 198/198 verdi (5 nuovi); frontend unit 129/140 (11 fail pre-esistenti non toccati, causa nota: `environment.apiUrl` assoluto non sostituito dal target `test` in `angular.json`); e2e `profile-push.spec.ts` 2/2 verdi + regressione `profile-theme`/`pwa-install` 3/3 verdi. Reviewer APPROVE — no Critical. 2 note non bloccanti: (1) test-send ritorna 404 sia per "nessun token" sia per "invio FCM fallito"; (2) nessun rate limiting su `/api/push/test`. > **PROSSIMO PASSO:** revisione umana. Quando approvi, lancia `/eq-approve US-029` (o aggiorna manualmente lo status a `DONE`).
**Blocked by:** US-028

**Story**
Come utente dell'app, voglio attivare o disattivare le notifiche push dalla pagina Profilo, così che possa controllare se ricevere notifiche del browser senza dover modificare i permessi del browser stesso.

**Demonstrates**
Nella pagina Profilo (introdotta in US-028) appare un toggle "Notifiche push" oltre al tema. Attivandolo, viene richiesto il permesso del browser (se non già concesso) e si registra la subscription esistente (`PushNotificationService`); disattivandolo, la subscription viene rimossa e l'utente non riceve più notifiche su quel device. Lo stato del toggle riflette sempre lo stato reale del permesso/subscription al caricamento della pagina. Se l'app è installata come PWA, accanto al toggle c'è un pulsante "Invia notifica di test" che invia all'utente stesso una push di prova, insieme a un testo breve che spiega come abilitare le notifiche a livello di sistema operativo e di app sul telefono. Se l'app non è installata, al posto del toggle/pulsante viene mostrata una guida/pulsante per installarla, con il testo che chiarisce che le notifiche push funzionano solo da app installata.

**Acceptance Criteria**

- [ ] La pagina Profilo mostra un toggle "Notifiche push" accanto al selettore tema
- [ ] Attivando il toggle quando il permesso browser non è ancora stato richiesto, viene mostrato il prompt nativo del browser; se l'utente nega, il toggle torna su "off" e mostra un messaggio che spiega come riattivarlo dalle impostazioni del browser
- [ ] Attivando il toggle con permesso già concesso, viene creata/registrata la subscription push tramite `PushNotificationService` esistente
- [ ] Disattivando il toggle, la subscription push lato browser/backend viene rimossa
- [ ] Al caricamento della pagina, il toggle riflette lo stato reale corrente (subscription attiva sì/no), non solo un valore salvato localmente
- [ ] Se il browser non supporta le notifiche push, il toggle è disabilitato con un messaggio esplicativo invece di fallire silenziosamente
- [ ] Se l'app è installata come PWA e la subscription è attiva, è presente un pulsante "Invia notifica di test" che invia una push di prova al device corrente dell'utente stesso
- [ ] Vicino al pulsante di test è presente un testo breve che spiega come abilitare le notifiche a livello di sistema operativo (es. permessi notifiche iOS/Android) e a livello di app, nel caso non arrivino
- [ ] Il testo chiarisce esplicitamente che le notifiche push funzionano solo se l'app è installata come PWA, non da semplice tab del browser
- [ ] Se l'app non è installata come PWA (rilevabile come in US-024), al posto del toggle/pulsante di test viene mostrato un pulsante o link guida per installare l'app, riusando il meccanismo di US-024 dove possibile

**Out of scope**

- Granularità per tipo di notifica (es. solo conferme partita vs solo inviti) — è on/off globale
- Notifiche push su più device contemporaneamente gestite da questa storia (ogni device gestisce la propria subscription)
- Invio di notifiche di test ad altri utenti (solo a se stessi)

**Open questions**

- (nessuna)

---

#### US-030: Modifica nome visualizzato dalla pagina Profilo

**Epic:** EP-005 | **Priority:** MEDIUM | **Story Points:** 3 | **Status:** DONE
**Blocked by:** US-028
**Review note (2026-06-26):** Codice in `src/Golp.Api/Endpoints/AuthEndpoints.cs`, `frontend/golp-app/src/app/auth/current-user.service.ts`, `frontend/golp-app/src/app/profile/profile.component.ts`. Test in `src/Golp.Tests/Integration/AuthIntegrationTests.cs` (+6 test), `frontend/golp-app/src/app/auth/current-user.service.spec.ts` (4), `frontend/golp-app/src/app/profile/profile.component.spec.ts` (+5), `frontend/golp-app/e2e/profile-name.spec.ts` (2 e2e). Reviewer APPROVE.
**Approved (2026-06-26):** Review umana OK.

**Story**
Come utente dell'app, voglio poter modificare il mio nome visualizzato dalla pagina Profilo, così che non resti fissato a quello scelto in fase di registrazione se cambia o lo sbaglio.

**Demonstrates**
Nella pagina Profilo (introdotta in US-028) appare un campo con il nome visualizzato attuale (`User.Name`), modificabile e salvabile. Dopo il salvataggio, il nuovo nome è visibile ovunque venga mostrato il nome utente (classifica, profilo, conferme partita) senza richiedere logout/login.

**Acceptance Criteria**

- [ ] La pagina Profilo mostra un campo "Nome visualizzato" precompilato con il valore attuale
- [ ] Salvando un nuovo valore valido, viene chiamato un endpoint dedicato che aggiorna `User.Name` e l'interfaccia mostra una conferma di salvataggio
- [ ] Il nuovo nome compare subito (senza re-login) in tutte le viste che mostrano il nome utente nella sessione corrente (es. header, classifica, lista partite)
- [ ] Un nome vuoto o solo spazi viene rifiutato con un messaggio di errore, nessuna chiamata API viene fatta
- [ ] Un nome troppo lungo (oltre il limite definito lato backend) viene rifiutato con messaggio di errore prima del salvataggio

**Out of scope**

- Modifica di email o password da questa storia (restano flussi separati)
- Cronologia/audit dei cambi nome
- Unicità del nome visualizzato tra utenti (non è un vincolo richiesto)

**Open questions**

- (nessuna)

---

#### US-031: Logout da tutti i device dal Profilo

**Epic:** EP-005 | **Priority:** MEDIUM | **Story Points:** 5 | **Status:** DONE
**Approved (2026-06-25):** Review umana OK.
**Review note (2026-06-25):** Backend: `User.SecurityStamp` (Guid) + migration; claim `security_stamp` nel JWT (`JwtService.GenerateToken`); validato in `Program.cs` (`OnTokenValidated`) contro DB. `POST /auth/logout-all` autenticato in `AuthEndpoints.cs`: rigenera stamp + `RevokeAllForUserAsync`. Frontend: `auth.service.ts` `logoutAllDevices()`, `profile.component.ts` con conferma inline a due step (nessun dialog riusabile esistente in progetto) + redirect login. Test: backend 207/207 verdi (5 nuovi unit/integration); frontend unit `profile.component` 16/16 verdi; `auth.service` 2 nuovi test falliscono per bug pre-esistente env (`environment.apiUrl`, stesso noto da US-029), logica corretta; e2e `profile-logout-all.spec.ts` 1/1 verde + regressione auth/profile-theme/profile-push 10/10 verdi. Reviewer APPROVE — no Critical. 1 nota non bloccante: query DB extra per ogni richiesta autenticata (validazione stamp), accettabile per MVP. > **PROSSIMO PASSO:** revisione umana. Quando approvi, lancia `/eq-approve US-031` (o aggiorna manualmente lo status a `DONE`).
**Blocked by:** US-028

**Story**
Come utente dell'app, voglio poter disconnettere tutte le sessioni attive su qualsiasi device dalla pagina Profilo, così che possa proteggere il mio account se temo accessi non autorizzati o ho perso un device.

**Demonstrates**
Nella pagina Profilo appare un'azione "Esci da tutti i device". Attivandola, tutti i JWT emessi finora per l'utente diventano invalidi (anche quello della sessione corrente), e l'utente viene riportato al login. Un nuovo login emette un token valido normalmente.

**Acceptance Criteria**

- [ ] Esiste un meccanismo di revoca lato backend (es. versione/`security stamp` su `User`, controllato ad ogni validazione JWT) dato che oggi i token non sono revocabili
- [ ] L'azione "Esci da tutti i device" richiede una conferma esplicita prima di eseguire (azione distruttiva per le sessioni)
- [ ] Dopo l'azione, qualsiasi richiesta autenticata con un token emesso prima della revoca viene rifiutata con 401, incluso quello del device che ha eseguito l'azione
- [ ] Dopo l'azione, l'utente che l'ha eseguita viene reindirizzato al login sul device corrente
- [ ] Un nuovo login dopo la revoca funziona normalmente ed emette un token valido

**Out of scope**

- Elenco dettagliato delle sessioni/device attivi con possibilità di revocarne una singola (qui è "tutte o nessuna")
- Notifica email all'utente quando l'azione viene eseguita

**Open questions**

- (nessuna)

---

#### US-032: Eliminazione account dal Profilo

**Epic:** EP-005 | **Priority:** MEDIUM | **Story Points:** 8 | **Status:** DONE
**Approved (2026-06-25):** Review umana OK.
**Review note (2026-06-25):** Backend: `POST /auth/me/delete` in `AuthEndpoints.cs` — verifica password (BCrypt), anonimizza `User` (nome/email/password), rimuove `CircleMembership`, annulla `Match` pending con l'utente, rigenera `SecurityStamp` + revoca refresh token (riuso US-031). Match confirmed storici intatti per costruzione. Frontend: `auth.service.ts` `deleteAccount(password)`, `profile.component.ts` con conferma a due step + password. Bug reale trovato e fixato in `auth.interceptor.ts`: il 401 di password-errata veniva trattato come token scaduto (refresh+retry+logout spurio) — escluso `/auth/me/delete` dal flusso refresh, con test di regressione. Test: backend 213/213 verdi (6 nuovi `AccountDeletionIntegrationTests`); frontend unit `profile.component` 21/21, `auth.interceptor` 5/5 verdi; e2e `profile-delete-account` 1/1 + regressione 10/10 verdi (8 fail in suite scollegate add-member/circle-awards/circle-match-history/invite, pre-esistenti, nessun file di quelle aree toccato). Reviewer APPROVE — no Critical. > **PROSSIMO PASSO:** revisione umana. Quando approvi, lancia `/eq-approve US-032` (o aggiorna manualmente lo status a `DONE`).
**Blocked by:** US-028

**Story**
Come utente dell'app, voglio poter eliminare il mio account dalla pagina Profilo, così che possa smettere di usare il servizio e non lasciare i miei dati personali accessibili.

**Demonstrates**
Nella pagina Profilo appare un'azione "Elimina account" che richiede conferma esplicita (es. ridigitare la password). Dopo la conferma, l'account viene anonimizzato (soft-delete: nome sostituito con "Utente eliminato", email/password invalidate, riga utente mantenuta) e l'utente non può più fare login con quelle credenziali. Le partite storiche confermate restano coerenti per gli altri 3 giocatori coinvolti (rating e storico non si rompono).

**Acceptance Criteria**

- [ ] L'azione "Elimina account" richiede conferma esplicita con re-inserimento password, non un solo click
- [ ] L'eliminazione è soft/anonimizzazione: la riga `User` resta nel DB ma email e password vengono invalidate/rimpiazzate e il nome visualizzato diventa "Utente eliminato" (nessuna FK rotta verso match/membership storici)
- [ ] Dopo l'eliminazione, login con le vecchie credenziali fallisce in modo esplicito
- [ ] Le `CircleMembership` dell'utente eliminato vengono rimosse: l'utente non appare più come membro in nessun circolo
- [ ] Le partite storiche `confirmed` in cui l'utente eliminato era coinvolto restano consultabili dagli altri 3 giocatori (nome mostrato "Utente eliminato"), senza alterare il loro rating già calcolato
- [ ] Partite `pending` che richiedono ancora la conferma dell'utente eliminato vengono gestite in modo esplicito (es. annullate o auto-confermate), non restano bloccate indefinitamente
- [ ] L'eliminazione è irreversibile dal punto di vista utente: non esiste un endpoint di "ripristino" account, anche se i dati restano anonimizzati in DB

**Out of scope**

- Periodo di grazia/soft-delete con possibilità di annullare l'eliminazione entro N giorni
- Export dei propri dati prima della cancellazione (GDPR data portability) — eventuale storia futura
- Hard delete fisico della riga utente (scelta esplicita: si anonimizza, non si rimuove)

**Open questions**

- (nessuna — confermata anonimizzazione/soft-delete, non hard delete)

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
**Review note (2026-06-29):** Codice in `src/Golp.Api/{Data/Entities/Sport.cs, Data/AppDbContext.cs, Services/{ISportsService,SportsService}.cs, Endpoints/CircleEndpoints.cs, Services/RatingService.cs}`, migration `20260624145528_AddSportsTable`, test in `src/Golp.Tests/{Services/SportsServiceTests.cs, Integration/CircleIntegrationTests.cs, +17 test factory fixes}`. Reviewer APPROVE.
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

#### US-036: Conferma esplicita di irreversibilità prima della forzatura del risultato

**Epic:** EP-002 | **Priority:** MEDIUM | **Story Points:** 2 | **Status:** DONE
**Blocked by:** US-013
**Review note (2026-06-26):** Stato inline a due step in `circle-match-history.component.ts/html`. Test unit in `.spec.ts`, e2e in `e2e/circle-match-history.spec.ts`. Reviewer APPROVE. > **PROSSIMO PASSO:** revisione umana. Quando approvi, lancia `/eq-approve US-036`.
**Approved (2026-06-26):** Review umana OK.

**Story**
Come proprietario del circolo, voglio una conferma esplicita con avviso di irreversibilità prima di forzare il risultato di una partita, così che non forzi una conferma per errore senza capire che l'azione non sarà più modificabile.

**Demonstrates**
Quando il proprietario preme "Forza conferma" su una partita `pending`, prima dell'esecuzione viene mostrato un passaggio di conferma esplicito (es. dialog) con il testo che l'azione è definitiva e la partita non potrà più essere modificata. Solo dopo la conferma esplicita la chiamata di forzatura viene eseguita; annullando il dialog non succede nulla.

**Acceptance Criteria**

- [ ] Cliccando "Forza conferma" appare un passaggio di conferma esplicito separato dal click iniziale (non un singolo click diretto sull'azione)
- [ ] Il testo del passaggio di conferma indica chiaramente che una volta forzata la partita non è più modificabile
- [ ] Annullando il passaggio di conferma, nessuna chiamata API viene effettuata e la partita resta `pending`
- [ ] Confermando, il comportamento esistente di US-013 (transizione a `confirmed`, aggiornamento rating, audit trail) resta invariato
- [ ] Il vincolo "partita forzata non più modificabile" è già vero lato backend (nessuna modifica al modello di stato richiesta) — questa storia copre solo l'avviso esplicito lato UI prima dell'azione

**Out of scope**

- Introdurre un meccanismo di modifica/riapertura partite forzate (resta non previsto, come da US-013)
- Nota/motivazione testuale obbligatoria per la forzatura (questione aperta separata in US-013)

**Open questions**

- (nessuna)

---

#### US-037: Pagina di dettaglio partita

**Epic:** EP-002 | **Priority:** MEDIUM | **Story Points:** 5 | **Status:** DONE
**Blocked by:** US-005
**Approved (2026-06-25):** Review umana OK.
**Review note (2026-06-25):** Backend in `src/Golp.Api/Endpoints/MatchEndpoints.cs` (membership check + campi dettaglio), frontend in `src/app/circles/match-detail/`, route in `app.routes.ts`, link in `circle-match-history.component.html`. Mockup in `docs/mockups/US-037/`. Test: 6 integration backend, 6 unit frontend, 22 spec storico, 3 e2e. Reviewer APPROVE. > **PROSSIMO PASSO:** revisione umana. Quando approvi, lancia `/eq-approve US-037` (o aggiorna manualmente lo status a `DONE`).

**Story**
Come giocatore, voglio poter cliccare su una partita nell'elenco e vedere una pagina di dettaglio con risultati, date e variazione di rating, così che possa capire esattamente come e quando una partita ha influito sul mio punteggio.

**Demonstrates**
Dall'elenco partite del circolo, cliccando su una partita (in qualsiasi stato: `pending`, `confirmed`, `disputed`) si naviga a una pagina dedicata che mostra: risultato per set/game o punteggio secondo lo sport, data di registrazione della partita, data di conferma (se confermata) e da chi è stata confermata l'ultima conferma necessaria (4° giocatore o forzatura del proprietario, vedi US-013), e la variazione di rating (delta) introdotta dalla partita per ciascuno dei 4 giocatori. Per partite `pending`, la pagina non mostra delta (non ancora calcolato).

**Acceptance Criteria**

- [ ] Cliccando una riga dell'elenco partite si naviga a una pagina dedicata `/circles/:circleId/matches/:matchId/detail` (o rotta equivalente, distinta da quella di conferma US-005)
- [ ] La pagina mostra il risultato della partita (set/game o punteggio secondo `point_unit` dello sport)
- [ ] La pagina mostra la data di registrazione della partita
- [ ] Per partite `confirmed`, la pagina mostra la data dell'ultima conferma che ha chiuso la partita e l'identità di chi l'ha eseguita (4° giocatore confermante, oppure proprietario in caso di forzatura US-013)
- [ ] Per partite `confirmed`, la pagina mostra la variazione di rating (delta) applicata a ciascuno dei 4 giocatori per effetto di questa partita
- [ ] Per partite `pending` o `disputed`, la sezione delta/data-conferma non viene mostrata (dato non esistente) invece di un valore vuoto o errato
- [ ] La pagina rispetta la multi-tenancy: un utente non membro del circolo non può accedere al dettaglio (403/404)

**Out of scope**

- Modifica dei dati della partita dalla pagina di dettaglio (resta read-only)
- Storico delle modifiche di rating per il giocatore nel tempo (grafico andamento) — eventuale storia futura
- Sostituire `MatchConfirmComponent` (US-005): quello resta il flusso di conferma per partite `pending`, questa è una vista di sola lettura aggiuntiva

**Open questions**

- (risolta in `/eq-plan`: derivabile da `MatchConfirmation`, `Match.ForceConfirmedById/At` e `Match.DeltaTeamXPlayerY` — nessuna nuova persistenza richiesta. Vedi `docs/planning/US-037.md`)

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

#### US-034: Margine corretto per partite pari su set o game

**Epic:** EP-003 | **Priority:** MEDIUM | **Story Points:** 5 | **Status:** DONE
**Approved (2026-06-24):** Review umana OK.
**Review note (2026-06-24):** `MatchEndpoints.cs` (validazione: pareggio set ammesso se i game decidono, 400 solo su vero pareggio totale), `RatingService.cs` (scoreRatio: peso set forzato a 0 se set pari, a 1 se game pari; floor ±1 in `ComputeDeltas` quando il margine reale arrotonderebbe a 0 — segno da `margin`, non da `isWinner`). Test: 3 integration (`MatchIntegrationTests`, `RatingServiceIntegrationTests`) + 3 unit (`RatingServiceTests`), suite completa 193/193 verde. Reviewer APPROVE. > **PROSSIMO PASSO:** revisione umana. Quando approvi: `/eq-approve US-034`.
**Blocked by:** US-012

**Story**
Come Marco, voglio che il calcolo del rating riconosca correttamente le partite finite in pareggio a livello di set o di game, così che il delta assegnato rifletta il reale margine della vittoria invece di trattare un pareggio parziale come una vittoria netta.

**Demonstrates**
Per uno sport a set (es. padel), se una partita finisce 1-1 nei set ma con game totali diversi, la creazione partita è ora ammessa (il vincitore è deciso dai game) e il delta ELO viene calcolato usando solo la differenza di game (il contributo "set" del margine è azzerato). Se invece la partita finisce con lo stesso numero di game totali ma set diversi, il delta viene calcolato usando solo la differenza di reti/punti (point_unit dello sport). Un delta che arrotonderebbe a 0 per una partita con vincitore reale viene forzato a ±1. Il vero pareggio totale (set pari e game pari) resta bloccato in creazione come oggi, quindi non genera mai un delta.

**Acceptance Criteria**

- [ ] La creazione partita per sport a set ammette un pareggio di set (es. 1-1) quando i game totali decidono un vincitore: in questo caso il vincitore è la squadra con più game totali
- [ ] La creazione partita resta bloccata con 400 solo per il vero pareggio totale (set pari E game totali pari) - invariato rispetto a oggi
- [ ] Per sport a set: se i set vinti dalle due squadre sono uguali (ma i game decidono), il margine usato nella formula ELO si basa esclusivamente sulla differenza di game totali, ignorando la componente set
- [ ] Per sport a set: se i game totali delle due squadre sono uguali (ma i set decidono), il margine usato nella formula ELO si basa esclusivamente sulla differenza di reti/punti (point_unit), ignorando la componente game
- [ ] Se il margine calcolato produce un delta che arrotonderebbe a 0 per una partita con un vincitore reale (set o game non entrambi pari), il delta minimo applicato è ±1 (con segno coerente: +1 al team vincente, -1 al perdente) - mai 0 quando esiste un vincitore
- [ ] Per sport senza set (es. burraco, basket2v2), il comportamento esistente non cambia: pareggio resta bloccato in creazione, formula basata solo su point_unit invariata
- [ ] La formula esistente (amplifier 0.7, K 32/48, rating iniziale 1000) resta invariata nei casi non di pareggio parziale

**Out of scope**

- Ricalcolo retroattivo dei rating storici già applicati con la formula precedente
- Modifica dei parametri amplifier/K/rating iniziale
- Esposizione della formula o del criterio di pareggio all'utente (algoritmo resta opaco)
- Ammissione del pareggio totale (per qualunque sport) — resta bloccato in creazione come oggi, quindi un delta esattamente 0 non si verifica mai in pratica
- Estensione del concetto di pareggio parziale a sport senza set (burraco, basket2v2)

**Open questions**

- (nessuna — ambiguità risolte: pareggio di set ammesso solo se i game decidono il vincitore; pareggio totale resta bloccato per tutti gli sport)

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

**Epic:** EP-006 | **Priority:** HIGH | **Story Points:** 8 | **Status:** DONE
**Approved (2026-07-01):** Review umana OK.
**Blocked by:** US-039, US-040
**Review note (2026-07-01):** Codice in `src/Golp.Api/Endpoints/QuickMatchEndpoints.cs`, `frontend/golp-app/src/app/circles/quick-match/quick-match.component.ts`. Test integration in `src/Golp.Tests/Integration/QuickMatchEndpointsTests.cs` (11 test). E2E in `frontend/golp-app/e2e/quick-match.spec.ts` (3 scenari). Reviewer APPROVE. **PROSSIMO PASSO:** revisione umana. Quando approvi: `/eq-approve US-041`.

**Story**
Come giocatore registrato, voglio registrare una partita con amici senza dover prima creare un circolo separatamente, così che posso iniziare a usare GOLP immediatamente per qualsiasi gruppo di gioco senza setup preliminare.

**Demonstrates**
Dalla dashboard, il pulsante "Registra Partita" avvia un flusso Quick Match in 4 passi: (1) scelgo lo sport; (2) seleziono i 3 altri giocatori tramite ricerca libera o chip suggeriti — i suggerimenti includono sia giocatori con cui ho già disputato partite sia membri dei miei circoli, ordinati per interazione più recente; ogni giocatore può essere aggiunto come utente registrato o come ospite (nome + contatto); (3) il backend verifica i circoli esistenti con due modalità distinte — **senza nuovi ospiti**: cerca circoli con esattamente questi 4 membri per quello sport (1 trovato → redirect obbligatorio, N trovati → picker, 0 trovati → crea nuovo); **con nuovi ospiti** (persone non ancora in DB): cerca circoli dove i giocatori noti condividono lo stesso sport e chiede "Registri in un circolo esistente o crei un nuovo gruppo?" con entrambe le opzioni sempre disponibili; (4) inserisco il risultato e invio. Il backend aggiunge gli ospiti nuovi al circolo scelto (o al circolo appena creato), crea gli utenti ghost e registra la partita in un'unica transazione atomica. Tutti i partecipanti ricevono notifica con link di conferma.

**Acceptance Criteria**

- [ ] Dalla dashboard esiste un pulsante primario "Registra Partita" visibile senza dover entrare in un circolo; il pulsante identico dentro un circolo usa il flusso classico (invariato)
- [ ] Il flusso Quick Match segue i passi: sport → selezione 3 giocatori → (check circolo) → risultato → (nome circolo se nuovo)
- [ ] I suggerimenti giocatori al passo 2 sono l'unione di: partecipanti a partite passate + membri dei circoli dell'utente; deduplicati e ordinati per recency
- [ ] Ogni giocatore può essere aggiunto come utente registrato (ricerca per nome/email) o come ospite (nome + telefono o email)
- [ ] **Modalità exact** (nessun nuovo ospite): il backend risolve eventuali ghost user per email/telefono e cerca circoli con esattamente questi 4 membri per quello sport
  - 1 circolo trovato → redirect obbligatorio con banner "Stai registrando in [Nome Circolo]"; nessun bypass possibile
  - N circoli trovati → picker esplicito con nome circolo e data ultima partita; l'utente sceglie
  - 0 circoli trovati → passo "Nome gruppo" appare dopo il risultato, pre-compilato e modificabile
- [ ] **Modalità partial** (almeno un ospite nuovo non in DB): il backend cerca circoli dove tutti i giocatori noti (utenti registrati + ghost già esistenti) condividono lo stesso sport; mostra picker CON opzione "Crea nuovo gruppo" sempre visibile; l'utente sceglie; il nuovo ospite viene aggiunto come membro al circolo scelto o a quello appena creato
- [ ] Il backend gestisce la creazione atomica: `Circle` (privato, `IsPrivate = true`) + `CircleMembership` per tutti (inclusi ghost) + eventuali `User` ghost + `Match`
- [ ] Il circolo creato è privato per default (`IsPrivate = true`): non appare in "Sfoglia Circoli"
- [ ] Tutti i giocatori ricevono notifica (push o email) al termine del flusso

**Out of scope**

- Impostazioni avanzate del circolo durante il quick match (join code, visibilità pubblica)
- Gestione dati legacy con circoli duplicati identici preesistenti (il picker copre il caso)

**Open questions**

- (nessuna)

---

#### US-042: Condivisione link conferma partita via WhatsApp o Web Share API

**Epic:** EP-006 | **Priority:** MEDIUM | **Story Points:** 3 | **Status:** DONE
**Blocked by:** US-040
**Review note (2026-07-01):** Codice in `src/Golp.Api/Endpoints/MatchEndpoints.cs`, `src/Golp.Api/Endpoints/QuickMatchEndpoints.cs`, `frontend/golp-app/src/app/circles/share-confirm/`, `record-match/`, `quick-match/`. Test: 2 integration (271 totali ✅), 13 unit ✅, 2 E2E ✅. Reviewer APPROVE.
**Approved (2026-07-01):** Review umana OK.

**Story**
Come registratore di una partita, voglio poter inviare il link di conferma ai giocatori tramite WhatsApp (con numero precompilato se disponibile) o via Web Share API, così che lo ricevono immediatamente e lo confermano con un solo tap.

**Demonstrates**
Dopo aver registrato una partita, la pagina di successo mostra per ogni partecipante un pulsante di condivisione. Se il partecipante ha un numero di telefono, il pulsante apre WhatsApp con numero e messaggio precompilati. Se non ha numero, appare un pulsante "Condividi link" che invoca `navigator.share()` con il testo e il link già pronti — il sistema operativo apre il selettore nativo (WhatsApp, Telegram, SMS, email, copia…). L'utente sceglie il canale e invia.

**Acceptance Criteria**

- [ ] Dopo la creazione di una partita, la pagina di successo mostra per ogni partecipante un pulsante di condivisione del link di conferma
- [ ] Se il partecipante ha numero di telefono: il pulsante apre `https://wa.me/<phone>?text=<messaggio_encoded>` con numero normalizzato in formato internazionale senza `+` (es. `393401234567`)
- [ ] Il messaggio precompilato contiene: nome del partecipante, sport, nome del circolo, link conferma con token (es. `golp.app/m/<token>`)
- [ ] Se il partecipante NON ha numero di telefono: il pulsante invoca `navigator.share({ title, text, url })` con messaggio e link già composti
- [ ] Se il browser non supporta `navigator.share` (es. desktop senza supporto): viene mostrato un campo di testo con il link da copiare manualmente
- [ ] Il link funziona senza che il registratore abbia salvato il numero in rubrica

**Out of scope**

- Invio automatico server-side via WhatsApp Business API
- Notifiche WhatsApp per eventi diversi dalla conferma partita (ranking, awards)
- Chatbot WhatsApp bidirezionale
- Verifica numero di telefono con OTP
- CTA di condivisione in punti diversi dalla pagina di successo post-registrazione (vedi US-043)

**Open questions**

- (nessuna)

---

#### US-043: CTA conferma WhatsApp/Share disponibile da tutti i punti di richiesta conferma

**Epic:** EP-006 | **Priority:** MEDIUM | **Story Points:** 2 | **Status:** TODO
**Blocked by:** US-042

**Story**
Come registratore di una partita, voglio poter ri-inviare il link di conferma ai giocatori che non hanno ancora confermato — non solo subito dopo la registrazione, ma anche dalla pagina di dettaglio partita e dalla lista partite in attesa — così che posso sollecitarli senza dover navigare altrove.

**Demonstrates**
Dalla pagina di dettaglio di una partita in stato `pending`, l'utente vede per ogni giocatore non-ancora-confermante un pulsante "Invia link conferma". Il comportamento è identico a US-042: WhatsApp diretto se ha numero, Web Share API se no, copia manuale come fallback. Stesso comportamento anche da un eventuale badge "in attesa" nella lista partite del circolo.

**Acceptance Criteria**

- [ ] La pagina di dettaglio partita (stato `pending`) mostra per ogni partecipante non confermante il pulsante di condivisione link (logica uguale a US-042)
- [ ] La lista partite del circolo mostra, accanto alle partite `pending`, un pulsante rapido "Sollecita conferme" che apre un mini-sheet con i pulsanti per i non-confermanti
- [ ] Il token nel link è lo stesso già generato per quella partita (non ne viene generato uno nuovo ad ogni tap)
- [ ] Il pulsante di condivisione è visibile solo al registratore della partita o al proprietario del circolo
- [ ] Il comportamento WhatsApp/Share/fallback-copia è identico a US-042

**Out of scope**

- Ri-generazione del token di conferma (il token esiste già da US-040)
- Notifiche push o email aggiuntive (già in US-006 e US-020)
- Interfaccia di sollecito per il giocatore non-registratore

**Open questions**

- (nessuna)

---

> **PROSSIMO PASSO:** avvia il piano tecnico della prima storia del growth loop con `/eq-plan US-042`.

---

#### US-044: Storico partite personale nella dashboard

**Epic:** EP-004 | **Priority:** HIGH | **Story Points:** 5 | **Status:** REVIEW
**Blocked by:** -
**Review note (2026-07-01):** Backend in `src/Golp.Api/Endpoints/MyMatchEndpoints.cs`, registrato in `Program.cs`. FE: `frontend/golp-app/src/app/dashboard/my-matches-page.component.ts`, route in `app.routes.ts`, link in `dashboard.component.ts`. Interfacce in `match.service.ts`. Test integration in `src/Golp.Tests/Integration/MyMatchesEndpointTests.cs` (7 test). E2E in `e2e/dashboard-match-history.spec.ts` (3 scenari). Suite 278/278 verde. Reviewer APPROVE. > **PROSSIMO PASSO:** revisione umana. Quando approvi, lancia `/eq-approve US-044`.

**Story**
Come giocatore, voglio vedere sulla dashboard un elenco di tutte le mie partite (di tutti i circoli), ordinate dalla più recente alla più vecchia, così che posso monitorare i miei risultati e delta ELO senza entrare nei singoli circoli.

**Demonstrates**
Apro la dashboard e vedo una lista paginata delle mie partite con: nome circolo, sport, data, score (set), esito Win/Loss, delta ELO (se disponibile), stato (confermata / in attesa / disputata). Posso filtrare con tre toggle: Tutte / In attesa / Contestate. Le partite Quick Match (circoli auto-creati) sono incluse.

**Acceptance Criteria**

- [ ] Nuovo endpoint `GET /matches/mine?page=1&pageSize=20` restituisce partite dell'utente autenticato su tutti i circoli, ordinate per `CreatedAt` desc, con campi: `matchId`, `circleId`, `circleName`, `sport`, `createdAt`, `status`, `winnerTeam`, `myTeam` (1 o 2), `sets` (array score), `myDelta` (int? — null se pending/disputed), `hasCurrentUserConfirmed`
- [ ] Response include `totalCount` e `page` per supportare paginazione lato client (load-more o pagine)
- [ ] La dashboard mostra la lista partite sotto i link di navigazione esistenti
- [ ] Ogni riga mostra: nome circolo, data, esito (Win/Loss o "—" se non confermata), score sintetico (es. "6-3 / 7-5"), delta ELO (es. "+12" verde / "-8" rosso / "—" se null), badge stato (confermata / in attesa / disputata)
- [ ] Tre toggle di filtro: **Tutte** (default) | **In attesa** (`pending`) | **Contestate** (`disputed`)
- [ ] Paginazione: bottone "Carica altre" appende la pagina successiva alla lista (infinite scroll-style, non replace)
- [ ] Partite Quick Match (in circoli auto-creati) incluse nella lista
- [ ] Se l'utente non ha partite, mostra empty state "Nessuna partita ancora"

**Out of scope**

- Dettaglio partita cliccabile (già US-037)
- Filtro per singolo circolo o per sport
- Grafici o trend ELO (feature separata)
- Notifiche push per nuove partite

**Open questions**

- (nessuna)

---

> **PROSSIMO PASSO:** avvia il piano tecnico con `/eq-plan US-044`.

---

#### US-045: Pulsante "Torna all'ultimo circolo" nella dashboard

**Epic:** EP-004 | **Priority:** MEDIUM | **Story Points:** 2 | **Status:** TODO
**Blocked by:** -

**Story**
Come giocatore, voglio vedere nella dashboard un pulsante che mi porta direttamente all'ultimo circolo in cui ho giocato, con il nome del circolo visibile sull'etichetta, così che posso tornare subito al contesto più recente senza navigare tra i miei circoli.

**Demonstrates**
Accedo alla dashboard e vedo un pulsante con scritto "⚡ Padel Roma" (o il nome del mio ultimo circolo). Cliccando vado a `/circles/{circleId}`. Se non ho mai giocato nessuna partita, il pulsante non appare.

**Acceptance Criteria**

- [ ] Nuovo endpoint `GET /matches/mine/last-circle` restituisce `{ circleId, circleName }` dell'ultimo circolo in cui l'utente ha giocato (ordinato per `Match.CreatedAt` desc), oppure 204 se nessuna partita esiste
- [ ] La dashboard mostra il pulsante solo se la chiamata restituisce un circolo (no 204)
- [ ] L'etichetta del pulsante mostra il nome del circolo (es. "Padel Roma")
- [ ] Click naviga a `/circles/{circleId}`
- [ ] Se non ho partite, il pulsante è assente (niente placeholder o skeleton)

**Out of scope**

- Mostrare l'ultimo circolo dove sono membro ma non ho giocato
- Storico degli ultimi N circoli
- Badge con partite pending nel pulsante (feature separata)

**Open questions**

- (nessuna)

---

> **PROSSIMO PASSO:** avvia il piano tecnico con `/eq-plan US-044` o `/eq-plan US-045`.

---

#### US-046: Branding visivo per ambiente (logo e icona manifest)

**Epic:** EP-005 | **Priority:** MEDIUM | **Story Points:** 2 | **Status:** DONE
**Approved (2026-07-01):** Review umana OK.
**Review note (2026-07-01):** Codice in `frontend/golp-app/src/` (environment files, styles.scss, app.component.ts, angular.json, manifest files, index files). Test in `src/app/app.component.spec.ts` (1 nuovo test, 6/6 pass). Reviewer APPROVE — Important fixato (rinomina `document` → `doc`).
**Blocked by:** -

**Story**
Come sviluppatore/amministratore, voglio che il logo nell'header e l'icona sul manifest siano diversi per ambiente (arancione in prod, giallo in test, verde in dev), così che sia immediatamente visibile in quale ambiente sto operando senza dover leggere URL o configurazioni.

**Demonstrates**
Avviando l'app nei tre ambienti, il logo in alto a destra mostra colori distinti: arancione (#FF6B35 o simile) in prod, giallo in test, verde in dev. Installando la PWA su mobile, l'icona home-screen riflette lo stesso colore ambiente.

**Acceptance Criteria**

- [ ] In ambiente `production` il logo nell'header è arancione (colore attuale — nessuna regressione)
- [ ] In ambiente `test` il logo nell'header è giallo
- [ ] In ambiente `dev` (development) il logo nell'header è verde
- [ ] L'icona nel `manifest.webmanifest` punta a un file immagine diverso per ciascuno dei tre ambienti
- [ ] Le icone manifest sono presenti in tutti i formati già usati (es. 192×192, 512×512)
- [ ] Il cambio di colore/icona è guidato da `environment.ts` (o equivalente Angular), non da un flag runtime in build

**Out of scope**

- Splash screen o tema colore dell'intera UI (solo logo + icona manifest)
- Ambienti aggiuntivi oltre dev/test/prod
- Generazione automatica delle icone (le icone colorate si creano manualmente o con script esterno)

**Open questions**

- (nessuna)

---

> **PROSSIMO PASSO:** avvia il piano tecnico con `/eq-plan US-046`.

---

#### US-047: Backend — supporto partite singolo (1v1)

**Epic:** EP-002 | **Priority:** HIGH | **Story Points:** 5 | **Status:** DONE
**Blocked by:** -
**Approved (2026-07-02):** Review umana OK.
**Review note (2026-07-02):** Codice in `src/Golp.Api/`, test in `src/Golp.Tests/Integration/SinglesMatchTests.cs`. Reviewer APPROVE.

**Story**
Come giocatore, voglio poter registrare una partita singolo (1v1) in un circolo il cui sport lo consente, così che il risultato venga convalidato dai 2 partecipanti e il ranking si aggiorni correttamente.

**Demonstrates**
Tramite API si registra una partita singolo in un circolo con sport `AllowsSingles=true`. La partita risulta `pending` con 2 slot giocatore invece di 4. Dopo 2 conferme lo status diventa `confirmed` e i rating ELO dei 2 giocatori si aggiornano. Registrare singolo su sport con `AllowsSingles=false` restituisce 400.

**Acceptance Criteria**
- [ ] La tabella `Sports` ha colonna `AllowsSingles` (bool, default `false`); gli sport che la supportano (padel, beachtennis) hanno `AllowsSingles=true` via migration seed
- [ ] `SportDto` espone `AllowsSingles` alla frontend
- [ ] `Match` entity: `Team1Player2Id` e `Team2Player2Id` diventano `Guid?` (nullable); aggiunta colonna `IsSingles` (bool, default `false`)
- [ ] Endpoint `POST /circles/{id}/matches`: accetta `isSingles: true` nel body; se `true`, valida che sport.AllowsSingles=true e che `team1` e `team2` abbiano esattamente 1 elemento ciascuno; se `false` o assente, comportamento invariato (2 player per squadra)
- [ ] Il numero di conferme richieste per `confirmed` è `IsSingles ? 2 : 4` — non hard-coded
- [ ] `RatingService`: per partite singolo, team rating = rating del singolo giocatore (no media); calcola 2 delta invece di 4; check `memberships.Count` usa il numero corretto in base a `IsSingles`
- [ ] Lista partite e dettaglio partita restituiscono correttamente `team1` e `team2` con 1 o 2 elementi in base a `IsSingles`
- [ ] I test di integrazione coprono: registrazione singolo valida, registrazione singolo su sport non supportato (400), conferma con 2 giocatori, calcolo ELO singolo

**Out of scope**
- Classifica separata per singolo vs doppio (ELO unificato — scelta consapevole)
- Modifiche al frontend (coperte da US-048)
- Nuovi sport: si abilitano via `AllowsSingles=true` su sport esistenti, non si aggiungono key nuove

**Open questions**
- (nessuna)

---

#### US-048: Frontend — registrazione e visualizzazione partite singolo

**Epic:** EP-002 | **Priority:** HIGH | **Story Points:** 3 | **Status:** DONE
**Blocked by:** US-047
**Approved (2026-07-02):** Review umana OK.
**Review note (2026-07-02):** Codice in `frontend/golp-app/src/app/circles/`, test in `*.spec.ts`. Reviewer APPROVE. 3 test pre-esistenti rimangono rossi (non causati da US-048).

**Story**
Come giocatore, voglio che il form di registrazione partita mostri 1 o 2 slot per squadra in base al formato scelto, e che la lista partite indichi chiaramente se una partita è stata giocata in singolo o doppio.

**Demonstrates**
In un circolo con sport `AllowsSingles=true`, il form "registra partita" mostra un toggle singolo/doppio. Scegliendo singolo, ogni squadra ha un solo campo giocatore. La partita salvata appare nella lista con un badge "1v1". Nei circoli con sport `AllowsSingles=false` il toggle non compare e il form rimane invariato.

**Acceptance Criteria**
- [ ] Il form "registra partita" legge `sport.allowsSingles` dall'API; se `false`, nessun cambiamento visivo rispetto all'attuale
- [ ] Se `allowsSingles=true`, compare un toggle/selector "Singolo / Doppio" (default: doppio)
- [ ] Scegliendo "Singolo", ogni squadra mostra 1 solo campo giocatore; scegliendo "Doppio", 2 campi (comportamento attuale)
- [ ] Il body della chiamata `POST /circles/{id}/matches` include `isSingles: true/false` in base alla selezione
- [ ] Nella lista partite del circolo, le partite singolo mostrano un badge o etichetta "1v1" distinguibile dal doppio
- [ ] Il dettaglio partita singolo mostra correttamente 1 giocatore per squadra (no slot vuoti o null visibili)
- [ ] Regressione: nei circoli con sport senza singolo, il flusso di registrazione è identico all'attuale

**Out of scope**
- Filtro lista partite per formato (singolo/doppio)
- Statistiche separate singolo vs doppio
- Quick Match in modalità singolo (estensione futura)

**Open questions**
- (nessuna)

---

## EP-007 — Campionato del Circolo

_Il circolo come campionato vivo: GOLP propone gli accoppiamenti migliori in base a chi è fisicamente presente e alimenta una classifica stagionale a punti solo positivi. Risolve la demotivazione del giocatore attivo scavalcato da chi non gioca e trasforma l'app da registro a organizzatore di serate. (da discussione classifica 2026-07-06, docs/proposte-classifica.txt opzione 17)_

#### US-049: Serata al circolo — proposta accoppiamenti dai presenti e punti campionato

**Epic:** EP-007 | **Priority:** MEDIUM | **Story Points:** 8 | **Status:** TODO
**Blocked by:** US-008

**Story**
Come giocatore presente al circolo, voglio segnalare chi c'è in questo momento e ricevere dall'app la proposta di accoppiamenti migliore per la serata, così che le partite giocate facciano avanzare un campionato a punti che premia chi gioca davvero.

**Demonstrates**
Quattro o più membri risultano presenti in una "serata" del circolo. L'app propone gli accoppiamenti (coppie e sfida) privilegiando le combinazioni non ancora giocate in stagione e, a parità, l'equilibrio dei rating. La partita viene giocata e confermata col flusso esistente: oltre al delta ELO, i giocatori ricevono punti campionato solo positivi (es. 3 vittoria / 1 sconfitta). La classifica campionato del circolo mostra i punti accumulati; chi non gioca resta a zero e non può essere scavalcato "da fermo".

**Acceptance Criteria**

- [ ] Un membro può aprire la "serata" del circolo e selezionare i presenti dalla lista membri (check-in per conto di tutti, un tap per giocatore)
- [ ] Con almeno 4 presenti, l'app propone accoppiamenti: criterio primario le combinazioni coppia/avversari meno giocate nella stagione corrente, tie-break l'equilibrio della somma rating tra le due squadre
- [ ] Con presenti non multipli di 4, l'app indica chi riposa nel turno e lo prioritizza nel turno successivo della stessa serata
- [ ] La proposta è modificabile prima di avviare la partita (suggerita, non vincolante)
- [ ] La partita creata dalla proposta segue il ciclo esistente pending → confirmed (conferma 4/4 o forzatura) e alla conferma assegna sia delta ELO sia punti campionato
- [ ] I punti campionato sono solo positivi (vittoria > sconfitta > 0) e non vengono mai sottratti; chi non gioca non accumula nulla
- [ ] Il circolo ha una classifica campionato separata dalla classifica rating, ordinata per punti stagionali
- [ ] Uno slot presente può essere un ospite (flusso ospite esistente): entra nel matchmaking con rating 1000

**Out of scope**

- Calendario e giornate fisse di campionato (il campionato avanza solo per serate opportunistiche)
- Chiusura stagione, premi di fine stagione, reset automatico dei punti (storia futura)
- Check-in automatico via geolocalizzazione o QR code
- Gestione multi-campo (assegnazione a campi specifici)
- Notifiche push "si sta formando una serata al circolo"

**Open questions**

- Check-in: solo una persona spunta i presenti (organizzatore di fatto) o ognuno può fare check-in da sé? Nel dubbio l'AC 1 assume il primo modello (più semplice)
- I punti campionato valgono solo per partite proposte dall'app o per tutte le partite confermate nella stagione (eventualmente con bonus per quelle proposte)?
- Valori punti: 3/1 fissi, o bonus per sconfitta combattuta (es. 2 punti se il margine è sotto soglia)?
- Durata stagione: allineata al "giocatore dell'anno" (anno solare) o configurabile per circolo?

---

#### US-050: Torneo one-shot del circolo (formato Americano/Mexicano)

**Epic:** EP-007 | **Priority:** MEDIUM | **Story Points:** 8 | **Status:** TODO
**Blocked by:** US-049

**Story**
Come organizzatore di un circolo, voglio creare un torneo in giornata (formato Americano o Mexicano) con membri e ospiti, così che l'app generi i round, tenga la classifica live e proclami un vincitore senza che io debba gestire nulla a mano.

**Demonstrates**
L'organizzatore crea un torneo indicando formato, numero campi, punti per round e partecipanti (membri + ospiti). L'app genera i round con rotazione dei compagni (Americano) o accoppiamenti da classifica corrente (Mexicano). I risultati inseriti dall'organizzatore valgono subito (auto-confirmed) e aggiornano la classifica live del torneo. A fine round finale l'app proclama il podio. Le partite del torneo non modificano il rating ELO; i partecipanti ricevono punti campionato stagionale (partecipazione + bonus piazzamento).

**Acceptance Criteria**

- [ ] Creazione torneo con: formato (Americano o Mexicano), numero campi, punti per round, lista partecipanti (membri del circolo e/o ospiti via flusso ospite esistente)
- [ ] L'app genera automaticamente tutti i round: in Americano rotazione dei compagni da schema fisso; in Mexicano accoppiamenti dal 2° round in base alla classifica corrente
- [ ] Ogni giocatore vede il proprio prossimo match: campo, compagno, avversari
- [ ] I risultati delle partite di torneo sono inseriti dall'organizzatore e diventano subito validi (auto-confirmed, nessuna conferma 4/4)
- [ ] Classifica live del torneo aggiornata a ogni risultato (somma punti individuali fatti nei round)
- [ ] Le partite di torneo non modificano il rating ELO dei giocatori
- [ ] Alla chiusura, l'app mostra il podio e assegna punti campionato stagionale: bonus per piazzamento + punti partecipazione a tutti
- [ ] Il ritiro di un partecipante a torneo in corso rigenera i round rimanenti senza invalidare i risultati già giocati

**Out of scope**

- Formati a coppie fisse, gironi + eliminazione, tabelloni knockout (evoluzione futura per tornei weekend)
- Tornei multi-circolo o aperti al pubblico
- Iscrizione self-service con pagamento
- Gestione tempi/prenotazione campi reale (l'app assegna il campo, non gestisce l'orologio)
- Proiezione/condivisione pubblica della classifica live (schermata dedicata futura)

**Open questions**

- Il torneo lo crea solo il proprietario del circolo o qualsiasi membro?
- ELO davvero intatto, o opzione per-circolo "il torneo pesa a K ridotto"?
- Valori punti campionato per piazzamento (es. 15/10/5 podio + 3 a tutti): da definire coi test user
- Torneo weekend (gironi sabato + finali domenica): serve nel primo rilascio o basta la giornata singola?

---

#### US-051: Scelta metodo di calcolo punteggio del circolo (ELO vs Game+Bonus)

**Epic:** EP-005 | **Priority:** MEDIUM | **Story Points:** 3 | **Status:** REVIEW
**Review note (2026-07-08):** Backend: `PUT /circles/{id}/rating-config` owner-only in `CircleEndpoints.cs` (whitelist RatingMethod, range 1-200/1-52 su finestre), DTO `GET /circles/me` e leaderboard estesi con `ratingMethod`/`gameBonusPoints`/finestre (i campi `Circle.RatingMethod`/`GameBonusWindowMatches`/`GameBonusWindowWeeks` erano già stati introdotti da US-052). Frontend: `CircleRatingConfigComponent` (nuovo, dialog owner-only in `my-circles`, card ELO/Game+Bonus + 2 input condizionali), badge metodo attivo in `circle-leaderboard`, `circle.service.ts` esteso con `updateRatingConfig`. Mockup in `docs/mockups/US-051/`. Test: 10 integration (`CircleRatingConfigEndpointTests`) + 4 unit Angular (`circle.service.spec.ts`) + 7 unit Angular (`circle-rating-config.component.spec.ts`) + 2 e2e Playwright (`circle-rating-config.spec.ts`) = 317/317 BE verdi, 224/227 FE verdi (3 fail pre-esistenti non correlati: `navigator.share` readonly in `invite-dialog`, notifiche push in ambiente di test). Reviewer APPROVE — 1 bug reale trovato e fixato via e2e: `selectedMethod`/`windowMatches`/`windowWeeks` erano field initializer letti da `@Input` prima che Angular li valorizzasse (sempre `'Elo'`/default); spostati in `ngOnInit`. > **PROSSIMO PASSO:** revisione umana. Quando approvi: `/eq-approve US-051`.
**Blocked by:** US-052

**Story**
Come proprietario di un circolo, voglio scegliere se il circolo usa il rating ELO attuale oppure il nuovo metodo Game+Bonus, così che possa adottare il sistema di punteggio più adatto al mio gruppo di gioco.

**Demonstrates**
Nella pagina di configurazione del circolo il proprietario vede un selettore "Metodo di calcolo punteggio" con due opzioni (ELO / Game+Bonus). Cambiando opzione, da quel momento in poi le partite confermate vengono valutate col nuovo metodo; lo storico dei delta già assegnati non viene ricalcolato. Se il metodo scelto è Game+Bonus, il proprietario vede anche i due parametri configurabili (finestra partite, finestra settimane) descritti in US-052.

**Acceptance Criteria**

- [ ] Solo il proprietario del circolo può cambiare il metodo di calcolo (autorizzazione lato API)
- [ ] Il circolo ha un campo persistito `RatingMethod` (default `Elo` per circoli esistenti e nuovi, retrocompatibile)
- [ ] Cambiare metodo non ricalcola le partite già confermate: si applica solo alle conferme successive al cambio
- [ ] Se il metodo attivo è Game+Bonus, la UI del circolo mostra i due parametri configurabili di US-052 con i loro valori correnti
- [ ] Cambiare metodo è reversibile in qualunque momento (nessun blocco dopo N partite)
- [ ] La classifica del circolo (US-008) mostra un'etichetta che indica quale metodo è attivo

**Out of scope**

- Ricalcolo retroattivo dello storico quando si cambia metodo
- Metodi di calcolo ulteriori oltre ELO e Game+Bonus
- Migrazione assistita o simulazione "cosa succederebbe se" prima di cambiare metodo

**Open questions**

- (nessuna)

---

#### US-052: Metodo di calcolo punteggio "Game+Bonus" con finestra rolling

**Epic:** EP-003 | **Priority:** MEDIUM | **Story Points:** 8 | **Status:** DONE
**Approved (2026-07-08):** Review umana OK.
**Review note (2026-07-08):** `Circle.RatingMethod`/`GameBonusWindowMatches`/`GameBonusWindowWeeks` + `Match.GameBonusWinnerPoints` (migration `AddGameBonusRatingMethod`, default Elo/30/6). Nuovo `GameBonusRatingService`/`IGameBonusRatingService` (punti base = diff. game + 1, bonus upset 10% arrotondato per eccesso su punteggio medio squadra in finestra), wired in `PrepareConfirmAsync`/`ForceConfirmMatchAsync`/`ConfirmViaTokenAsync` con branch su `circle.RatingMethod`. Leaderboard (`GetCircleLeaderboardAsync`) estesa con `gameBonusPoints` + `ratingMethod` quando il metodo è attivo, via `GameBonusRatingService.GetWindowScoresAsync` (finestra N partite ∩ M settimane, SQL-side). Test: 13 unit (`GameBonusRatingServiceTests`) + 5 integration (`GameBonusConfirmTests`, `GameBonusLeaderboardEndpointTests`) = 307/307 verdi, zero regressioni. Reviewer APPROVE (1 Critical trovato e fixato in review: migration generava default 0/"" invece di 30/6/"Elo" — corretto con `HasDefaultValue` esplicito in `AppDbContext` + rigenerazione migration). > **PROSSIMO PASSO:** revisione umana. Quando approvi: `/eq-approve US-052`.
**Blocked by:** US-007

**Story**
Come Marco iscritto a un circolo che usa il metodo Game+Bonus, voglio che il mio punteggio rifletta quanto ho dominato la partita e sia premiato se batto una squadra più forte, ma calcolato solo sulle partite recenti, così che la classifica racconti il mio momento di forma attuale e non tutta la mia storia.

**Demonstrates**
Alla conferma di una partita in un circolo con metodo Game+Bonus attivo, la squadra vincente riceve (differenza game tra le due squadre) + 1 punto vittoria; la squadra perdente riceve 0 punti. Se la squadra vincente aveva la classifica inferiore prima della partita, riceve un bonus aggiuntivo pari al 10% (arrotondato per eccesso) della differenza di classifica tra le due squadre. La classifica del circolo è la somma dei punti ottenuti nelle sole partite che rientrano contemporaneamente nella finestra delle ultime N partite (rolling, default 30) e nella finestra delle ultime M settimane (default 6): una partita che esce da una delle due finestre smette di contare, e i punti totali del giocatore si ricalcolano di conseguenza.

**Acceptance Criteria**

- [ ] Punteggio base: squadra vincente = (game vinti − game persi) + 1; squadra perdente = 0; per sport a set si usa il totale game su tutti i set (stesso criterio di US-012)
- [ ] Bonus upset: se la classifica Game+Bonus della squadra vincente (prima della partita) era inferiore a quella della squadra perdente, si aggiunge ai punti della partita `ceil(0.10 × differenza_classifica)`; nessun bonus se vince la squadra già in vantaggio o a parità
- [ ] La classifica Game+Bonus di ogni giocatore è la somma dei punti delle sue partite confermate che rientrano sia nelle ultime N partite del circolo sia nelle ultime M settimane da oggi
- [ ] Quando una partita esce dalla finestra (per numero o per tempo) i suoi punti non contano più nella classifica corrente, senza necessità di un'azione manuale
- [ ] N (numero partite, default 30) e M (settimane, default 6) sono parametri impostabili dal proprietario del circolo (vedi US-051 per la UI di configurazione)
- [ ] Cambiare N o M ricalcola immediatamente la classifica corrente usando le nuove finestre, senza toccare i punti già registrati per ogni partita
- [ ] Partite pending o disputed non contribuiscono al punteggio Game+Bonus
- [ ] Ogni giocatore vede il proprio punteggio Game+Bonus per partita (analogo al delta `+N pt` di US-009), calcolato con la sua squadra
- [ ] La classifica Game+Bonus è indipendente per giocatore all'interno del circolo (non è per coppia)

**Out of scope**

- Applicazione del metodo Game+Bonus a più circoli con parametri diversi in un'unica vista aggregata
- Storico dell'evoluzione della classifica Game+Bonus nel tempo
- Coesistenza dei due metodi sullo stesso circolo in parallelo (un circolo ha un solo metodo attivo alla volta — vedi US-051)
- Bonus/malus per margini particolarmente ampi oltre a quanto già catturato dalla differenza game

**Open questions**

- ~~Riferimento bonus upset~~ Risolto: si usa il punteggio Game+Bonus corrente di ogni squadra; se non ci sono partite pregresse la differenza è 0 (nessun bonus possibile)
- ~~Punteggio squadra~~ Risolto: media dei punteggi dei due giocatori della coppia (coerente con ELO team_rating = media)
- Il ricalcolo massivo della classifica dopo un cambio di N o M ha un limite di scala accettabile (es. circoli con migliaia di partite storiche)?

---

#### US-053: Rimozione "Aggiungi membro" — unico ingresso non-self-service è l'ospite in partita

**Epic:** EP-006 | **Priority:** MEDIUM | **Story Points:** 3 | **Status:** DONE
**Approved (2026-07-08):** Review umana OK.
**Review note (2026-07-08):** Rimossi bottone "+ Giocatore"/`AddMemberDialogComponent`/`checkOrAddMember` (frontend) e `AddMemberAsync`+route (backend, `CircleEndpoints.cs`). Test: `CircleIntegrationTests.AddMemberEndpointTests` sostituito con 1 test che verifica 405 su `POST /circles/{id}/members` (route GET sullo stesso path resta mappata). Suite completa 309/309 verdi, frontend 217/220 (3 fail pre-esistenti su push-notification, non correlati). Reviewer APPROVE (0 Critical, 1 Suggestion cosmetica applicata). Verifica visiva frontend in dev non eseguita (estensione browser non connessa) — da fare in review umana. > **PROSSIMO PASSO:** revisione umana (incl. check visivo my-circles). Quando approvi: `/eq-approve US-053`.
**Blocked by:** -

**Story**
Come proprietario di un circolo, voglio avere un solo modo chiaro per far entrare qualcuno che non giocherà subito da solo (il link di invito) e un solo modo per aggiungere qualcuno mentre registro una partita (l'ospite), così che non debba più scegliere tra due bottoni che fanno quasi la stessa cosa.

**Demonstrates**
Nella schermata `my-circles` resta un solo bottone di acquisizione membri per l'owner: "Invita" (link self-service). Il bottone "Aggiungi membro" e il relativo dialog scompaiono. L'unico modo per far entrare nel circolo una persona nominata (email/telefono) senza che sia lei stessa a usare il link resta l'aggiunta come ospite durante la registrazione di una partita (quick-match o record-match), che già crea la `CircleMembership`.

**Acceptance Criteria**
- [ ] Il bottone "Aggiungi membro" e `AddMemberDialogComponent` sono rimossi da `my-circles` (componente e riferimenti in `MyCirclesComponent`, incluso `openAddMember`/`closeAddMember`)
- [ ] Il bottone "Invita" (link self-service) resta invariato e continua a funzionare come oggi
- [ ] Il flusso ospite in quick-match/record-match resta invariato (nessuna regressione sulla creazione di `CircleMembership` per gli ospiti)
- [ ] L'endpoint `POST /circles/{id}/members` (`AddMemberAsync`) viene deprecato o rimosso lato backend, coerentemente con la decisione presa in fase di piano tecnico (vedi Open questions)
- [ ] Nessuna funzionalità di attivazione account via email introdotta da `AddMemberAsync` viene silenziosamente persa per gli utenti già in stato pending creati da tale flusso in passato (dato esistente non deve rompersi)

**Out of scope**
- Unificare visivamente invite-link e ospite in un unico componente condiviso
- Modificare il comportamento di `ResolveOrCreateGuestAsync` o della logica di attivazione guest già esistente
- Introdurre un nuovo flusso di onboarding pre-partita (esplicitamente escluso, vedi AN-002)

**Open questions**
- L'endpoint `POST /circles/{id}/members` va rimosso del tutto o solo deprecato (mantenuto per compatibilità dati storici)? Da decidere in `/eq-plan US-053`.
- Cosa succede agli utenti `IsActivated=false` creati in passato da `AddMemberAsync` che non hanno mai attivato l'account? Verificare in fase di piano se serve una migrazione o restano invariati.

**Fonte:** [AN-002](analysis/AN-002.md)

---

#### US-054: Statistiche personali — vittorie/sconfitte, game vinti/persi, tendenza ultime 10 partite

**Epic:** EP-004 | **Priority:** MEDIUM | **Story Points:** 5 | **Status:** DONE
**Approved (2026-07-08):** Review umana OK.
**Review note (2026-07-08):** Estesa `GetMyStatsAsync` (`StatsEndpoints.cs`) con `Include(m => m.Sets)` + accumulo `matchesWon/Lost`, `gamesWon/Lost` (stesso criterio somma-set di `GameBonusRatingService`) e `recentForm` (ultime 10 partite confermate, ordine cronologico, `OrderBy(CreatedAt).TakeLast(10)`). Frontend: `CircleStatsResponse` esteso, 3 nuovi blocchi in `CircleStatsComponent` (record partite/game, badge tendenza), `isEmpty` ridefinito su `matchesWon+matchesLost===0`. Test: 4 nuovi integration (`PlayerStatsEndpointTests`, 13/13 verdi) + 2 nuovi unit Angular (8/8 verdi su `circle-stats.component.spec.ts`). Suite completa: backend 313/313, frontend 219/222 (3 fail pre-esistenti push-notification non correlati). Reviewer APPROVE (0 Critical, 0 Important). Verifica visiva manuale non eseguita in sessione (da fare in review umana). > **PROSSIMO PASSO:** revisione umana (incl. check visivo pagina stats). Quando approvi: `/eq-approve US-054`.
**Blocked by:** US-011

**Story**
Come Marco, voglio vedere nella mia pagina statistiche quante partite ho vinto e perso, quanti game ho vinto e perso in totale, e come sto andando nelle ultime 10 partite, così che capisca il mio andamento recente oltre a "con chi gioco meglio".

**Demonstrates**
Nella pagina statistiche personali del circolo (`/circles/:circleId/stats`, accanto a "miglior compagno"/"avversario più ostico" di US-011), Marco vede tre nuovi blocchi: totale partite vinte/perse nel circolo, totale game vinti/persi nel circolo, e una sequenza delle ultime 10 partite confermate (es. V-V-P-V-P-P-V-V-V-P) con indicazione visiva vittoria/sconfitta in ordine cronologico.

**Acceptance Criteria**

- [ ] Conteggio partite vinte e perse: solo partite `confirmed` del circolo corrente, per il giocatore loggato
- [ ] Conteggio game vinti e persi: somma dei game fatti/subiti dalla squadra del giocatore su tutte le sue partite confermate nel circolo (per sport a set, somma su tutti i set, stesso criterio di US-012)
- [ ] Tendenza ultime 10 partite: le 10 partite confermate più recenti del giocatore nel circolo, ordinate dalla più vecchia alla più recente, ciascuna marcata vinta o persa
- [ ] Se il giocatore ha meno di 10 partite confermate, la tendenza mostra solo quelle disponibili (nessun placeholder per le mancanti)
- [ ] Nessuna partita confermata nel circolo → i tre blocchi mostrano uno stato vuoto coerente con quello già usato in US-011, non errori
- [ ] Le statistiche sono per-circolo: cambiando circolo (se il giocatore è in più circoli) i tre blocchi si aggiornano di conseguenza

**Out of scope**
- Grafici storici o andamento del rating nel tempo (solo sequenza V/P, non un grafico a linee)
- Confronto della tendenza tra giocatori diversi
- Estendere la tendenza oltre le ultime 10 partite (no configurazione del numero)

**Open questions**
- Nessuna

---

#### US-055: Pagina spiegazione Game+Bonus e simulatore esteso ai due metodi

**Epic:** EP-003 | **Priority:** MEDIUM | **Story Points:** 5 | **Status:** REVIEW
**Review note (2026-07-08):** Backend: estratto `GameBonusRatingService.ComputeMatchPoints` (puro, riusato da `CalculateAndApplyAsync` e dal nuovo simulatore), `SimulateGameBonusEndpoints.cs` (`POST /api/simulate-game-bonus`, pubblico), registrato in `Program.cs`. Frontend: `rating-method.util.ts` (`ratingInfoPath`), `game-bonus-info/` (component + service + html + scss, analogo a `elo-info/` di US-017), route `game-bonus-info`, `circle-leaderboard` aggiornata per usare `ratingInfoPath(ratingMethod)` invece del link fisso a `/elo-info`. Dashboard lasciata invariata (nessun circolo di riferimento, vedi piano). Test: 5 nuovi unit `ComputeMatchPoints` + 9 integration `SimulateGameBonusEndpointTests` (backend 328/328 verdi); `rating-method.util.spec.ts`, `game-bonus-info.component.spec.ts` (6 casi), 2 nuovi casi in `circle-leaderboard.component.spec.ts` (frontend 229/232 verdi, 3 fail pre-esistenti su push-notification non correlati); e2e `game-bonus-info.spec.ts` scritto (non eseguito in sessione, richiede backend+frontend avviati). Reviewer APPROVE — nessun Critical. Suggestion non bloccante: `EloInfoComponent.submit()` (US-017) ha un bug pre-esistente indipendente da questa storia — il gate `form.invalid` include il FormArray `sets` sempre `required` anche in modalità "Risultato unico", quindi il bottone di submit risulta sempre disabilitato in quel modo; il nuovo `GameBonusInfoComponent` evita il problema con un `isValid()` dedicato per modalità, ma `EloInfoComponent` non è stato toccato (fuori scope). > **PROSSIMO PASSO:** revisione umana (incl. verifica e2e con server avviati). Quando approvi: `/eq-approve US-055`.
**Blocked by:** US-051, US-052

**Story**
Come Marco iscritto a un circolo che usa il metodo Game+Bonus, voglio leggere una spiegazione semplice di come funziona il mio punteggio e simulare una partita ipotetica con quel metodo, così che capisca l'impatto delle mie vittorie senza dover interpretare la spiegazione dell'ELO che non si applica al mio circolo.

**Demonstrates**
Esiste una pagina di spiegazione dedicata al metodo Game+Bonus (analoga a `/elo-info` di US-017: testo semplice + simulatore). Il link "Come funziona il rating?"/"Simula una partita" mostrato in un circolo punta sempre alla pagina coerente col `RatingMethod` di quel circolo: ELO → pagina ELO esistente, Game+Bonus → nuova pagina. Il simulatore della pagina Game+Bonus calcola punteggio base (differenza game + 1) ed eventuale bonus upset a partire da input inseriti dall'utente (game vinti/persi, punteggio Game+Bonus corrente delle due squadre), con la stessa formula usata da `GameBonusRatingService` lato backend.

**Acceptance Criteria**

- [ ] Nuova pagina (es. `/game-bonus-info`) con sezione testuale che spiega in linguaggio non tecnico: punteggio base = differenza game + 1 punto vittoria, perdente 0; bonus upset se la squadra vincente aveva classifica inferiore; classifica calcolata solo su finestra rolling (ultime N partite / ultime M settimane)
- [ ] Il simulatore della nuova pagina accetta in input i game delle due squadre (o dei set, per sport a set — stesso criterio di somma di US-012) e i punteggi Game+Bonus correnti delle due squadre, e mostra il punteggio partita assegnato a ciascuna squadra (base + bonus se applicabile), calcolato lato client con la stessa formula di `GameBonusRatingService`
- [ ] Ovunque nell'app compaia oggi il link a `/elo-info` (leaderboard, dashboard, per un dato circolo) il link punta invece a `/game-bonus-info` se quel circolo ha `RatingMethod = GameBonus`, e a `/elo-info` se `RatingMethod = Elo`
- [ ] La formula non è mai esposta come tale nella UI del circolo al di fuori di questa pagina dedicata (stessa regola UX di US-017 per l'ELO)
- [ ] Se l'utente non è in nessun circolo con Game+Bonus attivo, la pagina resta comunque raggiungibile direttamente (non è protetta da un guard legato al metodo)

**Out of scope**

- Storico simulazioni salvate
- Simulazione della finestra rolling (N partite / M settimane) o di più partite in sequenza
- Migrazione automatica dei link già condivisi/salvati che puntano a `/elo-info`

**Open questions**

- (nessuna)

---

#### US-056: Revisione algoritmo Game+Bonus — vittoria per set, non per game totali

**Epic:** EP-003 | **Priority:** HIGH | **Story Points:** 5 | **Status:** DONE
**Approved (2026-07-08):** Review umana OK.
**Review note (2026-07-08):** Riscritta `GameBonusRatingService.ComputeMatchPoints` con nuova firma su lista set: `basePoints = (setsWon - setsLost) + gameDiffPerSetVinto` (soli set vinti dal vincitore). Fallback set vuoti (sport senza set) collassa esattamente sul comportamento pre-US-056. `SimulateGameBonusEndpoints` non riceve più `Team1Won` dal client: il vincitore è derivato server-side dai set vinti (elimina la classe di bug originale). Frontend `game-bonus-info.component.ts`/`.service.ts`/`.html` aggiornati di conseguenza (validazione su set vinti pari, testo esplicativo). Test: 8 nuovi/riscritti unit `GameBonusRatingServiceTests` (21/21 verdi, incl. caso reale 6-4/6-4/1-6 e caso set pareggiato ignorato), 9 integration `SimulateGameBonusEndpointTests` riscritti, 8 unit Angular `game-bonus-info.component.spec.ts` (incl. fix pre-esistente `provideRouter` mancante). Suite completa: backend 333/333 (1 fail pre-esistente non correlato, `GameBonusConfirmTests` su codice uncommitted di US-055 sui delta ELO), frontend 232/233 (2 fail pre-esistenti non correlati, `CircleMatchHistoryComponent`/`InviteDialogComponent`). Reviewer APPROVE (0 Critical). 2 Important non bloccanti risolti in follow-up: test esplicito per set pareggiato ignorato, commento su duplicazione validazione client/server. > **PROSSIMO PASSO:** revisione umana. Quando approvi: `/eq-approve US-056`.
**Blocked by:** US-052, US-055

**Story**
Come Marco iscritto a un circolo che usa il metodo Game+Bonus e gioca uno sport a set (padel, beach tennis), voglio che il punteggio della partita rifletta chi ha vinto più set, così che vincere la partita non venga mai penalizzato da una somma di game totali sfavorevole dovuta a un set perso largo.

**Demonstrates**
Partita 6-4, 6-4, 1-6: la squadra vince 2 set su 3 (match vinto) ma la somma game totale (13 vs 16) è sfavorevole. Con l'algoritmo attuale la squadra vincente riceve punti pari o inferiori alla perdente (bug). Dopo questa storia, la squadra vincente riceve sempre punti positivi, calcolati sui set vinti e sul game diff dei soli set vinti — mai penalizzata da un set perso largo. Il simulatore pubblico (`/game-bonus-info`, US-055) mostra lo stesso comportamento corretto, incluso il calcolo automatico di chi ha vinto la partita in modalità "per set".

**Acceptance Criteria**

- [ ] Per sport a set: `basePoints = (setsWon - setsLost) + gameDiffPerSetVinto`, dove `setsWon`/`setsLost` sono i set vinti/persi dalla squadra vincente del match, e `gameDiffPerSetVinto` è la somma di (game vinti - game persi) calcolata SOLO sui set vinti dalla squadra vincente (i set persi dal vincitore non contribuiscono né penalizzano)
- [ ] Per sport senza set (basket2v2, burraco) o risultato "unico": la formula produce lo stesso risultato numerico della formula attuale (game diff + 1) — nessuna regressione
- [ ] Match già confermati con `GameBonusWinnerPoints` già persistito non vengono ricalcolati (append-only, invariato rispetto a US-052)
- [ ] Il simulatore pubblico (`SimulateGameBonusEndpoints` + pagina `/game-bonus-info` di US-055) usa la stessa nuova formula, con lo stesso comportamento sui set vinti dal vincitore
- [ ] Nella pagina simulatore, modalità "per set": chi ha vinto la partita è determinato dal conteggio dei set vinti (maggioranza), non dalla somma dei game totali — corregge il bug per cui una squadra che vince 2 set su 3 ma ha meno game totali risultava "perdente" nel simulatore
- [ ] Nella pagina simulatore, modalità "risultato unico": il vincitore resta quello indicato direttamente dall'input (nessun cambiamento, non esistono set multipli da contare)
- [ ] Il testo esplicativo della pagina `/game-bonus-info` (US-055) viene aggiornato per descrivere la nuova formula (set vinti + game diff dei soli set vinti), non più "differenza game totale + 1"

**Out of scope**
- Ricalcolo retroattivo dei punti già persistiti sui match confermati prima di questa storia
- Modifiche al bonus upset (invariato, US-052)
- Modifiche alla finestra rolling N/M o alla classifica aggregata (invariate, US-052)

**Open questions**
- Nessuna

---

#### US-057: Chip giocatori raggruppate per circolo nella registrazione partita

**Epic:** EP-002 | **Priority:** MEDIUM | **Story Points:** 3 | **Status:** REVIEW
**Review note (2026-07-08):** Storia riscopata da `record-match` a `quick-match` (screenshot corrispondeva a quest'ultimo). Backend `QuickMatchEndpoints.GetSuggestionsAsync` ora espone `circles: [{id, name}]` per suggerimento (co-membership con l'utente corrente, nessun leak cross-tenant: solo circoli propri dell'utente). Frontend `quick-match.component.ts` raggruppa client-side via nuovo getter `groupedSuggestions` (gruppo residuo "Giocati di recente" per suggerimenti senza circolo comune, poi circoli in ordine alfabetico); template e CSS aggiornati per header di gruppo. Test: 3 nuovi backend (`QuickMatchEndpointsTests`, 14/14 verdi), 5 nuovi unit frontend (`quick-match.component.spec.ts`, nuovo file), 1 e2e (`quick-match.spec.ts`, verde). Suite completa dopo fix dei fallimenti pre-esistenti non correlati (richiesti separatamente dall'utente): backend 336/336, frontend 238/238 — entrambe verdi al 100%. Fix collaterali fuori scope US-057 ma necessari per suite verde: `GameBonusConfirmTests` (assert stale, i delta sono ora intenzionalmente valorizzati anche per Game+Bonus); `InviteDialogComponent.shareLink()` non chiudeva più il dialog dopo condivisione riuscita (bug reale, aggiunto `.then(() => this.close())`) e relativo test riscritto con `fakeAsync`/`tick()`; `CircleMatchHistoryComponent` — test puntava a un selettore CSS morto (`.match-date` non è più un link) e il marcatore "(Tu)" era stato perso in un refactor del template (ripristinato, rimosso metodo `teamNames()` orfano). Self-review: nessun Critical, nessun leak multi-tenant (circoli esposti sono solo quelli dell'utente stesso). > **PROSSIMO PASSO:** revisione umana. Quando approvi: `/eq-approve US-057`.
**Blocked by:** -

**Story**
Come Marco che registra una partita ed è membro di più circoli, voglio che nello step 2 (Squadre) le chip dei giocatori selezionabili siano raggruppate per circolo di appartenenza con etichetta visibile del circolo, così che io sappia sempre da quale circolo sto prendendo ciascun giocatore prima di aggiungerlo a una squadra.

**Demonstrates**
Nello step "Squadre" di `record-match`, sotto il campo "Cerca giocatore...", la lista di chip non è più un blocco unico indifferenziato: è organizzata in sezioni, una per circolo, ciascuna con un'etichetta testuale del nome del circolo sopra le chip dei suoi membri. Se l'utente digita nel campo di ricerca, il filtro si applica dentro ciascun gruppo mantenendo il raggruppamento (i gruppi senza risultati si nascondono).

**Acceptance Criteria**
- [ ] Le chip giocatore nello step 2 di "Registra Partita" sono raggruppate per circolo di appartenenza del giocatore rispetto all'utente corrente
- [ ] Ogni gruppo mostra un'etichetta con il nome del circolo sopra le sue chip
- [ ] Un giocatore membro di più circoli in comune con l'utente compare una volta per ciascun circolo condiviso (una chip per gruppo), non una sola chip ambigua
- [ ] Il campo "Cerca giocatore..." filtra le chip mantenendo il raggruppamento per circolo; i gruppi senza corrispondenze restano nascosti
- [ ] Il comportamento di selezione/aggiunta di una chip a Squadra A/B resta invariato rispetto a oggi
- [ ] Se l'utente è membro di un solo circolo, viene mostrato comunque il gruppo con la relativa etichetta (nessun caso speciale "circolo singolo senza etichetta")

**Out of scope**
- Filtri o ordinamento aggiuntivi dei circoli (es. per rating, per attività recente)
- Modifiche al flusso "Aggiungi come ospite"
- Modifiche alla logica di calcolo o alle chiamate API esistenti per il recupero dei membri

**Open questions**
- Nessuna

---

## EP-008 — Amministrazione di Sistema
*Strumenti per lo staff tecnico GOLP per supporto e diagnosi cross-circolo, fuori dal perimetro normale multi-tenant.*

#### US-058: Ruolo super admin di sistema

**Epic:** EP-008 | **Priority:** MEDIUM | **Story Points:** 3 | **Status:** REVIEW
**Review note (2026-07-09):** Codice in `src/Golp.Api/{Data/Entities/User.cs,Services/{JwtService.cs,IJwtService.cs,ClaimsPrincipalExtensions.cs},Endpoints/{AuthEndpoints.cs,AdminEndpoints.cs},Program.cs}`, migration `AddIsSuperAdminToUser`, test in `src/Golp.Tests/{Services/JwtServiceTests.cs,Integration/AdminEndpointsTests.cs}`. Reviewer APPROVE (nessun Critical; 2 punti Important/Suggestion non bloccanti: `ValidateToken` non ripropaga claim custom — verificare path refresh token; `Results.Forbid()` senza body di errore coerente con lo stile progetto). Suite completa 341/341 verde. > **PROSSIMO PASSO:** revisione umana. Quando approvi: `/eq-approve US-058`.
**Blocked by:** -

**Story**
Come gestore tecnico della piattaforma GOLP, voglio che esista un ruolo "super admin" globale assegnabile a uno o più account utente, così che solo persone autorizzate abbiano accesso a funzioni di amministrazione di sistema fuori dal perimetro di un singolo circolo.

**Demonstrates**
Un utente marcato come super admin (flag a livello di account, non di `CircleMembership`) ottiene un claim/ruolo aggiuntivo nel JWT dopo login. Un utente normale non vede né può richiamare nessuna funzione di super admin, nemmeno per URL diretta.

**Acceptance Criteria**
- [ ] Esiste un campo persistito a livello di `User` (non di circolo) che marca un account come super admin
- [ ] L'assegnazione del ruolo non è self-service da UI: richiede intervento diretto sul dato (script/DB), nessun endpoint pubblico per auto-promuoversi
- [ ] Il JWT di un super admin contiene un claim distinto verificabile lato backend
- [ ] Gli endpoint di amministrazione di sistema (introdotti in US-059) rifiutano con 403 chi non ha il claim, anche con token valido di un utente normale
- [ ] Un super admin continua a operare normalmente come utente/giocatore nei circoli di cui è già membro (il ruolo è additivo, non sostitutivo)

**Out of scope**
- UI per gestire l'elenco dei super admin (assegnazione resta manuale via DB/script)
- Livelli intermedi di admin (solo super admin / utente normale, nessuna via di mezzo)

**Open questions**
- Nessuna

---

#### US-059: Impersonazione di un utente tramite email

**Epic:** EP-008 | **Priority:** MEDIUM | **Story Points:** 5 | **Status:** REVIEW
**Review note (2026-07-09):** Codice in `src/Golp.Api/{Services/{JwtService.cs,IJwtService.cs},Endpoints/AdminEndpoints.cs}` (POST /admin/impersonate) e `frontend/golp-app/src/app/{auth/{auth.service.ts,super-admin.guard.ts},admin/impersonate/,shared/impersonation-banner/,app.component.ts,app.routes.ts}`. Test in `src/Golp.Tests/{Services/JwtServiceTests.cs,Integration/AdminEndpointsTests.cs}` e `frontend/.../*.spec.ts`. Reviewer APPROVE (nessun Critical; 2 Important applicati: guard 400 su email vuota/null, commenti chiarificatori su campo `token` duplicato e claim email durante impersonazione). Nessun mockup generato (utente ha scelto di saltare `/eq-design`, UI minimale riusa classi/token CSS globali esistenti). Suite completa: backend 347/347, frontend 251/251 verdi. Nota: fino a US-060 il super admin non ha modo di uscire dall'impersonazione se non rifacendo login — comportamento esplicitamente accettato nel piano. > **PROSSIMO PASSO:** revisione umana. Quando approvi: `/eq-approve US-059`.
**Blocked by:** US-058

**Story**
Come super admin, voglio poter impersonare un utente specifico inserendo la sua email, così che io possa vedere l'app esattamente come la vede lui per fare supporto o diagnosticare un problema segnalato.

**Demonstrates**
Il super admin ha un punto di accesso (pagina o comando) dove inserisce l'email di un utente esistente. Da quel momento l'app opera con l'identità di quell'utente: dashboard, circoli, partite, rating visti sono quelli dell'utente impersonato, non quelli del super admin.

**Acceptance Criteria**
- [ ] Il super admin può avviare l'impersonazione inserendo l'email di un utente esistente nel sistema
- [ ] Se l'email non corrisponde a nessun utente, il sistema mostra un errore chiaro e non genera alcun token
- [ ] Durante l'impersonazione, tutte le chiamate API autenticate operano con i dati e i permessi dell'utente impersonato (stesso `circle_id` scoping normale)
- [ ] L'interfaccia mostra in modo permanente e visibile un indicatore "stai impersonando [nome utente]" per tutta la durata della sessione impersonata
- [ ] L'utente impersonato non riceve alcuna notifica dell'impersonazione in corso (operazione trasparente per lui)
- [ ] L'impersonazione non richiede né consente di conoscere o reimpostare la password dell'utente target

**Out of scope**
- Impersonazione simultanea di più utenti nella stessa sessione browser
- Modifica dei dati dell'utente impersonato durante l'impersonazione (in questa storia si assume sola visualizzazione/uso normale, non un modo speciale)

**Open questions**
- Nessuna

---

#### US-060: Uscita da impersonazione e log di audit

**Epic:** EP-008 | **Priority:** MEDIUM | **Story Points:** 3 | **Status:** REVIEW
**Review note (2026-07-09):** Codice in `src/Golp.Api/{Data/Entities/ImpersonationAuditLog.cs,Data/AppDbContext.cs,Endpoints/AdminEndpoints.cs,Services/ClaimsPrincipalExtensions.cs}` (POST /admin/impersonate/end, log fail-closed su avvio), migration `AddImpersonationAuditLog`, e `frontend/golp-app/src/app/{auth/auth.service.ts,shared/impersonation-banner/}`. Test in `src/Golp.Tests/{Services/ClaimsPrincipalExtensionsTests.cs,Integration/AdminEndpointsTests.cs}` e `frontend/.../*.spec.ts`. Reviewer APPROVE (nessun Critical; 1 Important non bloccante: impersonazioni multiple concorrenti dello stesso admin/target senza chiusura possono lasciare righe di log orfane — segnalato come miglioramento futuro, non richiesto dagli AC di questa storia). Suite completa: backend 354/354, frontend 255/255 verdi. > **PROSSIMO PASSO:** revisione umana. Quando approvi: `/eq-approve US-060`.
**Blocked by:** US-059

**Story**
Come super admin, voglio poter terminare l'impersonazione in corso con un solo click e sapere che ogni impersonazione viene tracciata, così che io torni sempre alla mia identità reale e resti una traccia verificabile di chi ha impersonato chi e quando.

**Demonstrates**
Dall'indicatore di impersonazione (US-059) è presente un pulsante "Esci da impersonazione" che riporta immediatamente alla sessione originale del super admin. Ogni inizio/fine impersonazione viene registrato in un log consultabile (super admin, utente impersonato, timestamp inizio, timestamp fine).

**Acceptance Criteria**
- [ ] Un pulsante "Esci da impersonazione" sempre visibile durante l'impersonazione termina la sessione impersonata e ripristina l'identità originale del super admin senza richiedere nuovo login
- [ ] Ogni avvio di impersonazione viene registrato con: super admin che l'ha avviata, utente target, timestamp
- [ ] Ogni fine di impersonazione (esplicita o per scadenza token) viene registrata con timestamp
- [ ] Il log di audit non è modificabile né cancellabile da endpoint applicativi (sola scrittura in append)
- [ ] Chiudere il browser o scadere il token durante l'impersonazione non lascia il super admin bloccato nell'identità dell'utente target al login successivo

**Out of scope**
- UI di consultazione del log di audit (in questa storia il log esiste ed è persistito, non richiesta una pagina dedicata per sfogliarlo)
- Alerting automatico su impersonazioni sospette

**Open questions**
- Nessuna

---

> **PROSSIMO PASSO:** invoca `/eq-implement US-060` per implementare uscita da impersonazione e log di audit.
