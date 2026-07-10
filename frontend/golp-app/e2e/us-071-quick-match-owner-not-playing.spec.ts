import { test, expect, request as playwrightRequest, type Page } from '@playwright/test';

const API = 'http://localhost:5120';
const uid = (prefix = 'u') => `${prefix}_${Date.now()}_${Math.random().toString(36).slice(2)}`;

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

test('US-071 — owner rimuove se stesso da Quick Match e registra la partita per il gruppo', async ({ page }) => {
  const ownerEmail = `${uid('u071_owner')}@e2e.test`;
  const owner = await registerUser(ownerEmail, 'Owner US071');
  const p2 = await registerUser(`${uid('u071_p2')}@e2e.test`, 'Us071Due');
  const p3 = await registerUser(`${uid('u071_p3')}@e2e.test`, 'Us071Tre');
  const p4 = await registerUser(`${uid('u071_p4')}@e2e.test`, 'Us071Quattro');
  const p5 = await registerUser(`${uid('u071_p5')}@e2e.test`, 'Us071Cinque');

  const circle = await createCircle(owner.token);
  await joinCircle(p2.token, circle);
  await joinCircle(p3.token, circle);
  await joinCircle(p4.token, circle);
  await joinCircle(p5.token, circle);

  await loginViaUI(page, ownerEmail);
  await page.goto('/match/quick');
  await page.click('.qm-sport-card:has-text("Basket 2v2")');
  await expect(page.locator('.qm-slot').first()).toBeVisible();

  // AC2: owner rimuove se stesso dallo slot 0 (prima bloccato su "io")
  await page.locator('.qm-slot').first().locator('.qm-slot-remove').click();
  await expect(page.locator('.qm-slot').first()).not.toHaveClass(/filled/);

  // Compone la squadra con 4 membri diversi dal proprietario
  for (const name of ['Us071Due', 'Us071Tre', 'Us071Quattro', 'Us071Cinque']) {
    await page.fill('.qm-search-input', name);
    await expect(page.locator(`.qm-chip:has-text("${name}")`)).toBeVisible({ timeout: 6000 });
    await page.click(`.qm-chip:has-text("${name}")`);
  }

  await page.waitForSelector('.qm-cta:not([disabled])', { timeout: 8000 });
  await page.click('.qm-cta');

  // AC4: circolo esistente riconosciuto (mode exact), niente form nome circolo
  await expect(page.locator('.qm-info-banner')).toBeVisible({ timeout: 4000 });
  await expect(page.locator('.qm-name-section')).not.toBeVisible();

  await page.fill('.score-single-input--t1', '21');
  await page.fill('.score-single-input--t2', '15');

  await page.waitForSelector('.btn-primary.qm-cta:not([disabled])');
  await page.click('.btn-primary.qm-cta');

  // AC4/AC5: partita registrata, owner riceve i 4 link di conferma pur non giocando
  await expect(page.locator('[data-testid="success-state"]')).toBeVisible({ timeout: 10000 });
});

test('US-071 — non proprietario non può registrare senza partecipare su circolo altrui', async ({ page }) => {
  const ownerEmail = `${uid('u071b_owner')}@e2e.test`;
  const owner = await registerUser(ownerEmail, 'Owner US071B');
  const memberEmail = `${uid('u071b_member')}@e2e.test`;
  const member = await registerUser(memberEmail, 'Member US071B');
  const p3 = await registerUser(`${uid('u071b_p3')}@e2e.test`, 'Us071bTre');
  const p4 = await registerUser(`${uid('u071b_p4')}@e2e.test`, 'Us071bQuattro');
  const p5 = await registerUser(`${uid('u071b_p5')}@e2e.test`, 'Us071bCinque');

  const circle = await createCircle(owner.token);
  await joinCircle(member.token, circle);
  await joinCircle(p3.token, circle);
  await joinCircle(p4.token, circle);
  await joinCircle(p5.token, circle);

  await loginViaUI(page, memberEmail);
  await page.goto('/match/quick');
  await page.click('.qm-sport-card:has-text("Basket 2v2")');
  await expect(page.locator('.qm-slot').first()).toBeVisible();

  // Il membro non proprietario rimuove se stesso: frontend filtra i circoli non posseduti
  await page.locator('.qm-slot').first().locator('.qm-slot-remove').click();

  for (const name of ['Owner US071B', 'Us071bTre', 'Us071bQuattro', 'Us071bCinque']) {
    await page.fill('.qm-search-input', name.split(' ')[0]);
    await expect(page.locator(`.qm-chip:has-text("${name.split(' ')[0]}")`)).toBeVisible({ timeout: 6000 });
    await page.click(`.qm-chip:has-text("${name.split(' ')[0]}")`);
  }

  await page.waitForSelector('.qm-cta:not([disabled])', { timeout: 8000 });
  await page.click('.qm-cta');

  // AC3: il circolo esistente non è più tra le opzioni (owner escluso) → si passa a "crea nuovo circolo"
  await expect(page.locator('.qm-info-banner')).not.toBeVisible({ timeout: 4000 });
  await expect(page.locator('.qm-name-section')).toBeVisible();
});
