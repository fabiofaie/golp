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
  const r = await ctx.post(`${API}/circles`, { data: { name: `RM_${uid()}`, sport } });
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

// ─── US-042: success page dopo record-match ──────────────────────────────────

test('US-042 — record-match con ospite telefono: success page mostra WhatsApp', async ({ page }) => {
  // Setup: owner + 2 membri + ospite con telefono
  const ownerEmail = `${uid('owner')}@e2e.test`;
  const owner = await registerUser(ownerEmail, 'Owner RM');
  const p2 = await registerUser(`${uid('p2')}@e2e.test`, 'Player Due');
  const p3 = await registerUser(`${uid('p3')}@e2e.test`, 'Player Tre');

  const circleId = await createCircle(owner.token, 'basket2v2');
  await joinCircle(p2.token, circleId);
  await joinCircle(p3.token, circleId);

  await loginViaUI(page, ownerEmail);
  await page.goto(`/circles/${circleId}/match/new`);

  // Wait for member list to load (4 selects should appear)
  await expect(page.locator('select').first()).toBeVisible({ timeout: 5000 });

  // Select owner in slot 0, p2 in slot 1, p3 in slot 2
  await page.locator('select').nth(0).selectOption(owner.id);
  await page.locator('select').nth(1).selectOption(p2.id);
  await page.locator('select').nth(2).selectOption(p3.id);

  // Slot 3 → switch to guest (4th guest toggle button)
  await page.locator('.slot-toggle-btn--guest').nth(3).click();

  // Wait for guest form to render
  const guestPhone = '+39340' + Math.floor(Math.random() * 9000000 + 1000000);
  const lastCard = page.locator('.player-slot-card').nth(3);
  const guestNameInput = lastCard.locator('input[placeholder="Nome *"]');
  const guestPhoneInput = lastCard.locator('input[placeholder="Telefono"]');
  await expect(guestNameInput).toBeVisible({ timeout: 3000 });
  await guestNameInput.fill('Ospite Telefono');
  await guestPhoneInput.fill(guestPhone);

  // Score for basket2v2 (single score)
  await page.locator('.score-single-input--t1').fill('21');
  await page.locator('.score-single-input--t2').fill('15');

  // Submit
  await page.click('button[type="button"]:has-text("Inserisci partita")');

  // Verify: success state visible (no redirect)
  await expect(page.locator('[data-testid="success-state"]')).toBeVisible({ timeout: 8000 });
  await expect(page).not.toHaveURL(/\/circles$/);

  // Verify: WhatsApp button present for ospite
  const waBtn = page.locator('[data-testid="btn-whatsapp"]').first();
  await expect(waBtn).toBeVisible({ timeout: 5000 });

  // href must contain wa.me with normalized phone
  const href = await waBtn.getAttribute('href');
  expect(href).toBeTruthy();
  expect(href).toContain('wa.me/');
  const normalizedPhone = guestPhone.replace(/\D/g, '');
  expect(href).toContain(normalizedPhone);
  expect(href).toContain('text=');
});

test('US-042 — record-match 4 membri: success page, nessun redirect a /circles', async ({ page }) => {
  const ownerEmail = `${uid('o2')}@e2e.test`;
  const owner = await registerUser(ownerEmail, 'Owner2');
  const p2 = await registerUser(`${uid('q2')}@e2e.test`, 'Player Q2');
  const p3 = await registerUser(`${uid('q3')}@e2e.test`, 'Player Q3');
  const p4 = await registerUser(`${uid('q4')}@e2e.test`, 'Player Q4');

  const circleId = await createCircle(owner.token, 'basket2v2');
  await joinCircle(p2.token, circleId);
  await joinCircle(p3.token, circleId);
  await joinCircle(p4.token, circleId);

  await loginViaUI(page, ownerEmail);
  await page.goto(`/circles/${circleId}/match/new`);

  await expect(page.locator('select').first()).toBeVisible({ timeout: 5000 });

  await page.locator('select').nth(0).selectOption(owner.id);
  await page.locator('select').nth(1).selectOption(p2.id);
  await page.locator('select').nth(2).selectOption(p3.id);
  await page.locator('select').nth(3).selectOption(p4.id);

  await page.locator('.score-single-input--t1').fill('21');
  await page.locator('.score-single-input--t2').fill('15');

  await page.click('button[type="button"]:has-text("Inserisci partita")');

  // Success state visible, no navigation
  await expect(page.locator('[data-testid="success-state"]')).toBeVisible({ timeout: 8000 });
  await expect(page.locator('text=Partita')).toBeVisible();
  await expect(page).not.toHaveURL(/\/circles$/);
});
