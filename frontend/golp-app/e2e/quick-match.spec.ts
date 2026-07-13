import { test, expect, request as playwrightRequest, type Page } from '@playwright/test';

const API = 'http://localhost:5120';
const uid = (prefix = 'u') =>
  `${prefix}_${Date.now()}_${Math.random().toString(36).slice(2)}`;

async function registerUser(email: string, name: string): Promise<{ token: string; id: string }> {
  const ctx = await playwrightRequest.newContext();
  const r = await ctx.post(`${API}/auth/register`, { data: { email, password: 'testpass123', name } });
  const body = await r.json();
  await ctx.dispose();
  const token = (body.token ?? body.accessToken) as string;
  const id = JSON.parse(atob(token.split('.')[1])).sub as string;
  return { token, id };
}

async function createCircle(token: string, sport = 'basket2v2'): Promise<string> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${token}` } });
  const r = await ctx.post(`${API}/circles`, { data: { name: `QM_${uid()}`, sport } });
  const body = await r.json();
  await ctx.dispose();
  return body.id as string;
}

async function joinCircle(token: string, circleId: string): Promise<void> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${token}` } });
  await ctx.post(`${API}/circles/${circleId}/join`);
  await ctx.dispose();
}

async function loginViaUI(page: Page, email: string): Promise<void> {
  await page.goto('/login');
  await page.fill('#email', email);
  await page.fill('#password', 'testpass123');
  await page.click('button[type="submit"]');
  await expect(page).toHaveURL(/dashboard/);
}

// ─── Scenario A: 4 utenti senza circolo condiviso → crea nuovo circolo (isPrivate) ──

test('ScenarioA — 4 utenti nessun circolo: crea circolo privato e registra partita', async ({ page }) => {
  const ownerEmail = `${uid('a_owner')}@e2e.test`;
  const owner = await registerUser(ownerEmail, 'Alpha Owner');
  // p2, p3, p4 non hanno circolo con owner → verranno aggiunti come ospiti

  await loginViaUI(page, ownerEmail);
  await page.goto('/match/quick');
  await expect(page).toHaveURL(/match\/quick/);

  // Step 1: select sport
  await page.click('.qm-sport-card:has-text("Basket 2v2")');
  await expect(page.locator('.qm-slot').first()).toBeVisible();

  // Slot 0 auto-filled with current user; add 3 guests (email required by backend)
  for (const [name, email] of [
    ['Ospite Alfa', `${uid('g1')}@e2e.test`],
    ['Ospite Beta', `${uid('g2')}@e2e.test`],
    ['Ospite Gamma', `${uid('g3')}@e2e.test`],
  ] as [string, string][]) {
    await page.click('.qm-add-guest-btn');
    await page.fill('.qm-guest-form input[type="text"]', name);
    await page.fill('.qm-guest-form input[type="email"]', email);
    await page.click('.qm-guest-form .btn-primary');
    await page.waitForTimeout(150);
  }

  // All 4 filled → auto check → no circles → "Avanti" enabled, goes to score
  await page.waitForSelector('.qm-cta:not([disabled])', { timeout: 6000 });
  await page.click('.qm-cta');

  // Score step
  await expect(page.locator('.score-single-input').first()).toBeVisible({ timeout: 4000 });
  // No banner (new circle), name input auto-filled
  await expect(page.locator('.qm-info-banner')).not.toBeVisible();
  await expect(page.locator('.qm-name-section')).toBeVisible();

  const inputs = page.locator('.score-single-input');
  await inputs.nth(0).fill('21');
  await inputs.nth(1).fill('15');

  await page.waitForSelector('.btn-primary.qm-cta:not([disabled])');
  await page.click('.btn-primary.qm-cta');

  // US-042: success state (no redirect)
  await expect(page.locator('[data-testid="success-state"]')).toBeVisible({ timeout: 10000 });
  await expect(page).toHaveURL(/match\/quick/);
});

// ─── Scenario B: 4 utenti stesso circolo → EXACT mode → banner obbligatorio ──

test('ScenarioB — 4 utenti stesso circolo: banner nome circolo, no nuovo circolo', async ({ page }) => {
  const ownerEmail = `${uid('b_owner')}@e2e.test`;
  const owner = await registerUser(ownerEmail, 'Beta Owner');
  const p2 = await registerUser(`${uid('b_p2')}@e2e.test`, 'Beta Due');
  const p3 = await registerUser(`${uid('b_p3')}@e2e.test`, 'Beta Tre');
  const p4 = await registerUser(`${uid('b_p4')}@e2e.test`, 'Beta Quattro');

  const circleId = await createCircle(owner.token, 'basket2v2');
  await joinCircle(p2.token, circleId);
  await joinCircle(p3.token, circleId);
  await joinCircle(p4.token, circleId);

  await loginViaUI(page, ownerEmail);

  await page.goto('/match/quick');
  await expect(page).toHaveURL(/match\/quick/);

  await page.click('.qm-sport-card:has-text("Basket 2v2")');

  // Suggestions include circle members
  await expect(page.locator('.qm-chip:has-text("Beta Due")')).toBeVisible({ timeout: 6000 });
  await page.click('.qm-chip:has-text("Beta Due")');
  await page.click('.qm-chip:has-text("Beta Tre")');
  await page.click('.qm-chip:has-text("Beta Quattro")');

  // 4 slots filled → auto check → EXACT 1 circle → selectedCircle set automatically
  await page.waitForSelector('.qm-cta:not([disabled])', { timeout: 8000 });
  await page.click('.qm-cta');

  // Score step: info banner must be visible with circle name
  await expect(page.locator('.qm-info-banner')).toBeVisible({ timeout: 4000 });
  await expect(page.locator('.qm-info-banner')).toContainText('Stai registrando in');

  // No circle name input (existing circle used)
  await expect(page.locator('.qm-name-section')).not.toBeVisible();

  const inputs = page.locator('.score-single-input');
  await inputs.nth(0).fill('21');
  await inputs.nth(1).fill('15');

  await page.waitForSelector('.btn-primary.qm-cta:not([disabled])');
  await page.click('.btn-primary.qm-cta');

  // US-042: success state (no redirect)
  await expect(page.locator('[data-testid="success-state"]')).toBeVisible({ timeout: 10000 });
});

// ─── US-057: chip raggruppate per circolo con etichetta ──────────────────────

test('US-057 — chip cloud mostra etichetta del circolo comune sopra i giocatori', async ({ page }) => {
  const ownerEmail = `${uid('d_owner')}@e2e.test`;
  const owner = await registerUser(ownerEmail, 'Delta Owner');
  const p2 = await registerUser(`${uid('d_p2')}@e2e.test`, 'Delta Due');

  const circleId = await createCircle(owner.token, 'basket2v2');
  await joinCircle(p2.token, circleId);

  await loginViaUI(page, ownerEmail);
  await page.goto('/match/quick');
  await page.click('.qm-sport-card:has-text("Basket 2v2")');

  // Chip group label with the circle name appears above the suggestion chip.
  const group = page.locator('.qm-chip-group', { hasText: 'Delta Due' });
  await expect(group.locator('.qm-chip-group-label')).toBeVisible({ timeout: 6000 });
  await expect(group.locator('.qm-chip:has-text("Delta Due")')).toBeVisible();
});

// ─── Scenario C: 3 utenti in circolo + 1 ospite → PARTIAL → picker → "Crea nuovo gruppo" ──

test('ScenarioC — 3 utenti + ospite nuovo: picker con Crea nuovo gruppo', async ({ page }) => {
  const ownerEmail = `${uid('c_owner')}@e2e.test`;
  const owner = await registerUser(ownerEmail, 'Gamma Owner');
  const p2 = await registerUser(`${uid('c_p2')}@e2e.test`, 'Gamma Due');
  const p3 = await registerUser(`${uid('c_p3')}@e2e.test`, 'Gamma Tre');

  const circleId = await createCircle(owner.token, 'basket2v2');
  await joinCircle(p2.token, circleId);
  await joinCircle(p3.token, circleId);
  // p4 not registered → will be added as a new guest

  await loginViaUI(page, ownerEmail);

  await page.goto('/match/quick');
  await expect(page).toHaveURL(/match\/quick/);

  await page.click('.qm-sport-card:has-text("Basket 2v2")');

  // Pick the 2 circle members from suggestions
  await expect(page.locator('.qm-chip:has-text("Gamma Due")')).toBeVisible({ timeout: 6000 });
  await page.click('.qm-chip:has-text("Gamma Due")');
  await page.click('.qm-chip:has-text("Gamma Tre")');

  // Add brand-new guest (not in DB at all)
  await page.click('.qm-add-guest-btn');
  await page.fill('.qm-guest-form input[type="text"]', 'Ospite Nuovo');
  await page.fill('.qm-guest-form input[type="email"]', `${uid('new_guest')}@e2e.test`);
  await page.click('.qm-guest-form .btn-primary');

  // 4 slots filled → PARTIAL check → at least 1 circle found → "Avanti" enabled
  await page.waitForSelector('.qm-cta:not([disabled])', { timeout: 8000 });
  await page.click('.qm-cta');

  // Picker step: "Crea nuovo gruppo" must appear
  await expect(page.locator('.qm-circle-new')).toBeVisible({ timeout: 5000 });
  await page.click('.qm-circle-new');

  // Score step: new group → no banner, name input visible
  await expect(page.locator('.qm-name-section')).toBeVisible({ timeout: 4000 });
  await expect(page.locator('.qm-info-banner')).not.toBeVisible();

  const inputs = page.locator('.score-single-input');
  await inputs.nth(0).fill('21');
  await inputs.nth(1).fill('15');

  await page.waitForSelector('.btn-primary.qm-cta:not([disabled])');
  await page.click('.btn-primary.qm-cta');

  // US-042: success state (no redirect)
  await expect(page.locator('[data-testid="success-state"]')).toBeVisible({ timeout: 10000 });
  expect(page.url()).not.toContain('/matches/');
});

// ─── US-075: guest con telefono → link WhatsApp nella success page (gap coperto da record-match-share.spec.ts) ──

test('US-075 — ospite con telefono: success page mostra link WhatsApp', async ({ page }) => {
  const ownerEmail = `${uid('e_owner')}@e2e.test`;
  const owner = await registerUser(ownerEmail, 'Epsilon Owner');
  const p2 = await registerUser(`${uid('e_p2')}@e2e.test`, 'Epsilon Due');
  const p3 = await registerUser(`${uid('e_p3')}@e2e.test`, 'Epsilon Tre');

  const circleId = await createCircle(owner.token, 'basket2v2');
  await joinCircle(p2.token, circleId);
  await joinCircle(p3.token, circleId);

  await loginViaUI(page, ownerEmail);
  await page.goto('/match/quick');
  await page.click('.qm-sport-card:has-text("Basket 2v2")');

  await expect(page.locator('.qm-chip:has-text("Epsilon Due")')).toBeVisible({ timeout: 6000 });
  await page.click('.qm-chip:has-text("Epsilon Due")');
  await page.click('.qm-chip:has-text("Epsilon Tre")');

  const guestPhone = '+39340' + Math.floor(Math.random() * 9000000 + 1000000);
  await page.click('.qm-add-guest-btn');
  await page.fill('.qm-guest-form input[type="text"]', 'Ospite Telefono QM');
  await page.fill('.qm-guest-form input[type="tel"]', guestPhone);
  await page.click('.qm-guest-form .btn-primary');

  await page.waitForSelector('.qm-cta:not([disabled])', { timeout: 8000 });
  await page.click('.qm-cta');

  // Ospite nuovo → check PARTIAL: picker con il circolo esistente da scegliere
  await page.waitForSelector('.qm-circle-item:not(.qm-circle-new), .score-single-input', { timeout: 5000 });
  if (await page.locator('.qm-circle-item:not(.qm-circle-new)').first().isVisible().catch(() => false)) {
    await page.click('.qm-circle-item:not(.qm-circle-new)');
  }

  await expect(page.locator('.score-single-input').first()).toBeVisible({ timeout: 6000 });
  const inputs = page.locator('.score-single-input');
  await inputs.nth(0).fill('21');
  await inputs.nth(1).fill('15');

  await page.waitForSelector('.btn-primary.qm-cta:not([disabled])');
  await page.click('.btn-primary.qm-cta');

  await expect(page.locator('[data-testid="success-state"]')).toBeVisible({ timeout: 10000 });

  const waBtn = page.locator('[data-testid="btn-whatsapp"]').first();
  await expect(waBtn).toBeVisible({ timeout: 5000 });
  const href = await waBtn.getAttribute('href');
  expect(href).toBeTruthy();
  expect(href).toContain('wa.me/');
  const normalizedPhone = guestPhone.replace(/\D/g, '');
  expect(href).toContain(normalizedPhone);
});

// ─── US-075: entrata da card circolo con ?circleId= preseleziona il circolo ──

test('US-075 — entrata con ?circleId= preseleziona il circolo (nessun picker)', async ({ page }) => {
  const ownerEmail = `${uid('f_owner')}@e2e.test`;
  const owner = await registerUser(ownerEmail, 'Foxtrot Owner');
  const p2 = await registerUser(`${uid('f_p2')}@e2e.test`, 'Foxtrot Due');
  const p3 = await registerUser(`${uid('f_p3')}@e2e.test`, 'Foxtrot Tre');
  const p4 = await registerUser(`${uid('f_p4')}@e2e.test`, 'Foxtrot Quattro');

  const circleId = await createCircle(owner.token, 'basket2v2');
  await joinCircle(p2.token, circleId);
  await joinCircle(p3.token, circleId);
  await joinCircle(p4.token, circleId);

  await loginViaUI(page, ownerEmail);
  await page.goto(`/match/quick?circleId=${circleId}`);
  await expect(page).toHaveURL(/match\/quick/);

  await page.click('.qm-sport-card:has-text("Basket 2v2")');
  await expect(page.locator('.qm-chip:has-text("Foxtrot Due")')).toBeVisible({ timeout: 6000 });
  await page.click('.qm-chip:has-text("Foxtrot Due")');
  await page.click('.qm-chip:has-text("Foxtrot Tre")');
  await page.click('.qm-chip:has-text("Foxtrot Quattro")');

  // EXACT + unico circolo compatibile → salta il picker, va dritto allo score
  await page.waitForSelector('.qm-cta:not([disabled])', { timeout: 8000 });
  await page.click('.qm-cta');
  await expect(page.locator('.score-single-input').first()).toBeVisible({ timeout: 4000 });
  await expect(page.locator('.qm-info-banner')).toBeVisible();
});

// ─── US-075: URL legacy /circles/:id/match/new redirige a /match/quick ──

test('US-075 — URL legacy /circles/:id/match/new redirige a /match/quick con circleId', async ({ page }) => {
  const ownerEmail = `${uid('g_owner')}@e2e.test`;
  const owner = await registerUser(ownerEmail, 'Golf Owner');
  const circleId = await createCircle(owner.token, 'basket2v2');

  await loginViaUI(page, ownerEmail);
  await page.goto(`/circles/${circleId}/match/new`);

  await expect(page).toHaveURL(new RegExp(`/match/quick\\?circleId=${circleId}`));
  await expect(page.locator('.qm-sport-card').first()).toBeVisible({ timeout: 5000 });
});
