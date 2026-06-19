# Requisito: Refresh token per sessione lunga

## Problema

Attualmente l'autenticazione usa un solo JWT con scadenza fissa (60 minuti, `Jwt:ExpiryMinutes`). L'utente che installa la PWA in home screen viene disconnesso ogni ora e deve rifare login. Il JWT inoltre non è revocabile: una volta emesso resta valido fino a scadenza, senza modo di invalidarlo lato server.

## Richiesta

Sostituire il singolo JWT con uno schema **access token + refresh token**:

- **Access token**: JWT di breve durata (es. 1h), come oggi. Usato per autenticare le chiamate API.
- **Refresh token**: token long-lived, persistito in DB, usato per ottenere un nuovo access token senza richiedere nuovo login.
  - Durata configurabile tramite parametro (es. `Jwt:RefreshTokenExpiryDays`), valore iniziale **90 giorni**.
  - La durata è **scorrevole (sliding)**: ogni utilizzo dell'app che comporta un refresh valido rinnova la scadenza di altri N giorni a partire da quel momento.
  - Se l'utente non usa l'app per più di N giorni consecutivi, il refresh token scade e serve un nuovo login.
  - Nessun limite massimo assoluto di durata: con un uso anche minimo (una volta ogni N giorni), la sessione resta attiva indefinitamente.
- **Revoca**: il refresh token deve poter essere invalidato (logout singolo device, logout da tutti i device, cambio password, sospetto furto/riuso).
- **Tracciamento minimo per sessione/device**: `CreatedAt`, `LastUsedAt`, `UserAgent`, per stimare utenti attivi e numero di device per utente.

## Vincoli

- Multi-tenancy: il refresh token non introduce nessuna eccezione alle regole di isolamento per `circle_id` già esistenti.
- Il parametro di durata (giorni) deve essere configurabile da `appsettings.json`, non hardcoded.

## Fuori scope

- Scadenza assoluta indipendente dall'uso (non richiesta).
- Sistema di analytics completo (funnel, eventi): il tracciamento serve solo a stimare utenti/dispositivi attivi, non sostituisce strumenti dedicati.
