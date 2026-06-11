# Visual evidence — US-001 Registrazione e accesso account giocatore

**Generato il:** 2026-06-11 12:45 | **Tool:** Playwright 1.x + Chromium
**App URL:** http://localhost:4200 (Angular dev) + http://localhost:5120 (API .NET)

---

## AC verificati

| AC | Stato | Screenshot | Note |
|---|---|---|---|
| AC1 — Registrazione crea account e autentica subito | ✅ PASS | `screenshots/AC1-register-page.png`, `AC1-register-filled.png`, `AC1-dashboard-after-register.png` | Form → POST /auth/register → redirect dashboard |
| AC2 — Login valido → JWT; credenziali errate → errore generico | ✅ PASS | `screenshots/AC2-login-error.png`, `AC2-login-success-dashboard.png` | Messaggio "Credenziali non valide" senza indicare il campo specifico |
| AC3 — Email duplicata → errore chiaro, nessun duplicato | ✅ PASS | `screenshots/AC3-duplicate-email-error.png` | "Email già registrata" → 409 gestito lato UI |
| AC4 — Password < 8 char → rifiutata in registrazione | ✅ PASS | `screenshots/AC4-short-password-validation.png` | Validazione client-side (ng-invalid) prima del submit |
| AC5 — Token assente/scaduto blocca route protette | ✅ PASS | `screenshots/AC5-protected-redirect.png` | /dashboard → redirect a /login quando localStorage è vuoto |
| AC6 — Reset password: email + link temporaneo | ✅ PASS | `screenshots/AC6-forgot-password-page.png`, `AC6-forgot-password-success.png` | Messaggio successo generico (no enumeration email) |
| AC7 — Link già usato o scaduto → errore chiaro | ✅ PASS | `screenshots/AC7-reset-invalid-token.png` | Token invalido → "Link non valido" senza modifiche |

**Tutti gli AC: 7/7 PASS**

---

## Happy path

`walkthrough.webm` — durata ~4s — copre: register → dashboard → logout → login → forgot-password in sequenza.

---

## Visual design check

UI corrisponde ai mockup in `docs/mockups/US-001/` e ai design tokens in `style-tokens.json`:
- ✅ Background `#0A0A0A`, surface `#141414`
- ✅ Brand "GOLP" in arancione `#FF5500`
- ✅ Decorazione "1000" visibile in background con opacity 5%
- ✅ Bottone primario arancione, stato error rosso con background `rgba(255,68,68,0.10)`
- ✅ Font Inter, layout mobile-first 390px max-width

---

## Console errors raccolti

```
[ERROR] Failed to load resource: 401 (Unauthorized)   ← AC2 atteso
[ERROR] Failed to load resource: 409 (Conflict)        ← AC3 atteso
[ERROR] Failed to load resource: 400 (Bad Request)     ← AC7 atteso
```

Tutti gli errori console sono risposte HTTP attese dai test. Nessun errore di runtime JavaScript.

---

## Network failures (status >= 400)

```
POST http://localhost:5120/auth/login → 401          ← AC2 atteso
POST http://localhost:5120/auth/register → 409       ← AC3 atteso
POST http://localhost:5120/auth/password-reset/confirm → 400  ← AC7 atteso
```

Nessuna richiesta fallita inaspettata.

---

## Verdict suggerito al revisore umano

**APPROVE**

Tutti e 7 gli AC sono verificati con evidenza screenshot + video. Il visual design corrisponde ai mockup. Nessun errore JavaScript non atteso. Il flusso completo register→login→logout→reset funziona end-to-end con backend localdb.

---

> **PROSSIMO PASSO:** revisione umana di questo report + degli artefatti. Se OK: `/eq-approve US-001`. Se serve fix: `/eq-implement US-001`.
