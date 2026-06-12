# Firebase Setup — Push Notifications (US-006)

Istruzioni manuali per configurare Firebase Cloud Messaging (FCM). Servono **una sola volta** per ambiente. Tempo stimato: ~10 minuti.

## Prerequisiti

- Un account Google
- .NET SDK installato (per `dotnet user-secrets`)

## 1. Crea il progetto Firebase

1. Vai su <https://console.firebase.google.com> e accedi.
2. **Add project** → nome: `golp` (o quello che preferisci).
3. Google Analytics: **non necessario** per FCM — puoi disabilitarlo.
4. Attendi la creazione e apri il progetto.

## 2. Registra la Web App

1. Nella overview del progetto: icona **`</>`** (Add app → Web).
2. Nickname: `golp-app`. **Non** selezionare Firebase Hosting.
3. **Register app** → Firebase mostra l'oggetto `firebaseConfig`. Copia questi valori (serviranno per `environment.ts` del frontend):
   - `apiKey`
   - `authDomain`
   - `projectId`
   - `messagingSenderId`
   - `appId`

## 3. Genera la VAPID key (Web Push certificate)

1. ⚙️ **Project settings** → tab **Cloud Messaging**.
2. Sezione **Web configuration** → **Web Push certificates** → **Generate key pair**.
3. Copia la chiave pubblica (inizia con `B...`). Questa è la **VAPID key**.

## 4. Scarica il Service Account (credenziali backend)

1. ⚙️ **Project settings** → tab **Service accounts**.
2. **Generate new private key** → conferma. Scarica il file JSON.
3. Rinominalo `serviceAccountKey.json` e salvalo **fuori dal repo** (es. `C:\Users\<tu>\secrets\golp\serviceAccountKey.json`).

> ⚠️ Questo file dà accesso admin al progetto Firebase. È già in `.gitignore`, ma non copiarlo mai dentro il repo.

## 5. Configura i user-secrets del backend

Dalla root del repo:

```powershell
dotnet user-secrets init --project src/Golp.Api
dotnet user-secrets set "Firebase:ServiceAccountKeyPath" "C:\Users\<tu>\secrets\golp\serviceAccountKey.json" --project src/Golp.Api
dotnet user-secrets set "Firebase:VapidPublicKey" "<VAPID key dello step 3>" --project src/Golp.Api
```

Verifica:

```powershell
dotnet user-secrets list --project src/Golp.Api
```

Output atteso:

```
Firebase:ServiceAccountKeyPath = C:\Users\<tu>\secrets\golp\serviceAccountKey.json
Firebase:VapidPublicKey = B...
```

## 6. Configura il frontend

In `frontend/golp-app/src/environments/environment.ts` (e `.development.ts`) compila `firebaseConfig` con i valori dello step 2 e `vapidKey` con la chiave dello step 3. Sono valori **pubblici** (finiscono nel bundle JS): possono stare nel repo.

## Produzione / CI

- Backend: invece del path al file, usa la variabile d'ambiente `Firebase__ServiceAccountJson` con il **contenuto** del JSON (vedi `Program.cs`).
- La VAPID public key via `Firebase__VapidPublicKey`.

## Note dev

- Il Service Worker **non si attiva con `ng serve`**. Per testare le push in locale: `npm run build` + servire `dist/` via `npx http-server -p 4200` (oppure `ng serve --ssl`).
- Le push reali richiedono Chrome/Edge con permesso notifiche concesso.
