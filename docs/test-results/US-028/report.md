# Visual evidence — US-028 Switch manuale tema chiaro/scuro

**Generato il:** 2026-06-23 16:50 | **Tool:** Playwright (chromium, Desktop Chrome 1280×720)
**App URL:** http://localhost:4200 (dev) — API http://localhost:5120

## AC verificati

| AC | Stato | Screenshot | Note |
|---|---|---|---|
| AC-1 — pagina Profilo con controllo tema | ✅ PASS | `screenshots/AC-1-profilo-tema-scuro.png` | Pagina raggiungibile da dashboard, toggle Scuro/Chiaro presente, "Scuro" attivo |
| AC-2 — default scuro senza scelta precedente | ✅ PASS | `screenshots/AC-2-dashboard-default-scuro.png` | Nessuna classe `theme-light`, rendering scuro corretto |
| AC-3 — cambio applicato immediatamente | ⚠️ PARZIALE | `screenshots/AC-3-profilo-tema-chiaro.png` | Classe `theme-light` applicata e `--color-bg` cambia a `#F7F5F2`; MA su viewport desktop (>600px) il box `.page` resta scuro → testo scuro su sfondo scuro, **illeggibile** |
| AC-3b — applicato cross-pagina | ⚠️ PARZIALE | `screenshots/AC-3b-dashboard-tema-chiaro.png` | Stesso difetto desktop: dashboard "Dashboard"/link quasi invisibili |
| AC-5 — persistenza dopo reload | ✅ PASS | `screenshots/AC-5-persistenza-dopo-reload.png` | Tema chiaro resta dopo reload (localStorage) |
| AC-4 — persistenza localStorage | ✅ PASS | (coperto da unit + e2e) | chiave `golp_theme` scritta |
| AC-6 — device diverso → default scuro | ✅ PASS | (unit ThemeService) | localStorage vuoto → `dark` |

## Happy path
`walkthrough.webm` — copre registrazione → dashboard scuro → Profilo → toggle chiaro → ritorno dashboard → reload.

## Console errors raccolti
- `console.log`: nessun errore console.

## Network failures (status >= 400)
- `network.log`: nessuna richiesta fallita.

## Difetto identificato (visivo, non funzionale)

Il meccanismo di tema **funziona correttamente** a livello di stato (classe `theme-light` applicata, `--color-bg` cambia, persistenza OK, zero errori). Tuttavia il rendering del tema chiaro è **rotto su viewport desktop (≥ 600px)**:

- `frontend/golp-app/src/styles.scss` ha `@media (min-width: 600px) { body { background: #050505; } }` — valore **hardcoded scuro**, non legato a `var(--color-bg)`.
- Il contenitore `.page` non ha un `background` esplicito, quindi su desktop mostra il body scuro `#050505`.
- In tema chiaro il testo usa `--color-text-primary: #1A1A1A` (scuro) → **testo scuro su sfondo scuro = illeggibile**.

Su viewport mobile (< 600px) il difetto non si presenta (la media query non si applica e `body` usa `var(--color-bg)` chiaro). Questo è esattamente il rischio "valori hardcoded fuori da `:root`" già documentato nei piani US-027 e US-028 come follow-up — la verifica visiva conferma che su desktop è **bloccante per l'usabilità**, non solo cosmetico.

### Fix suggerito (per reopen)
Rendere tema-aware i valori hardcoded fuori da `:root`:
- `@media (min-width:600px) { body { background: ... } }` → usare una variabile (es. nuovo `--color-bg-deep` con valore chiaro nel blocco `:root.theme-light`).
- Dare a `.page` un `background: var(--color-bg)` esplicito così non dipende dal body.
- Estendere lo stesso trattamento agli altri hardcoded noti (`.score-input #0F0F0F/#3A3A3A`, `.feedback-icon`).

## Fix applicato (reopen 2026-06-23 16:55)

Il difetto è stato corretto rendendo tema-aware i valori hardcoded:
- Nuovi token `--color-bg-deep` (`#050505` dark / `#EDEAE5` light), `--color-input-bg`, `--color-input-border` in entrambi i blocchi `:root` + `style-tokens.json`.
- `@media (min-width:600px) { body { background: var(--color-bg-deep); } }` (era `#050505`).
- `.page { background: var(--color-bg); }` esplicito (non dipende più dal body).
- `.score-input` / `.score-single-input` ora usano `var(--color-input-bg)` / `var(--color-input-border)`.

**Re-verifica visiva:** `screenshots/AC-3-FIXED-profilo-tema-chiaro.png` e `AC-3b-FIXED-dashboard-tema-chiaro.png` mostrano entrambe le pagine leggibili in tema chiaro su desktop (card bianca, testo scuro, bottoni chiari). E2e `profile-theme.spec.ts` 2/2 verdi, suite unit 123/132 (9 pre-esistenti). Build OK.

## Verdict suggerito al revisore umano
**APPROVE**

Stato, persistenza e rendering visivo del tema sono ora corretti su mobile e desktop. Difetto bloccante risolto. Nota minore non bloccante: il bottone "Chiaro" attivo ha uno sfondo leggermente spento (accent-dim su superficie chiara) — cosmetico, testo leggibile.

---

> **PROSSIMO PASSO:** revisione umana di questo report. Se OK → `/eq-approve US-028`.
