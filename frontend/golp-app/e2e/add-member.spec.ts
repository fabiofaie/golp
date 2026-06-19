import { test, expect, request as playwrightRequest } from '@playwright/test';

const API = 'http://localhost:5120';
const uniqueEmail = (prefix = 'u') => `${prefix}_${Date.now()}_${Math.random().toString(36).slice(2)}@e2e.test`;

async function registerUser(email: string, name: string): Promise<string> {
  const ctx = await playwrightRequest.newContext();
  const r = await ctx.post(`${API}/auth/register`, { data: { email, password: 'testpass123', name } });
  const body = await r.json();
  await ctx.dispose();
  return body.token as string;
}

async function createCircle(token: string): Promise<string> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${token}` } });
  const r = await ctx.post(`${API}/circles`, { data: { name: `E2E_ADDM_${Date.now()}`, sport: 'padel' } });
  const body = await r.json();
  await ctx.dispose();
  return body.id as string;
}

async function loginInBrowser(page: import('@playwright/test').Page, email: string): Promise<void> {
  await page.goto('http://localhost:4200/login');
  await page.fill('input[type="email"]', email);
  await page.fill('input[type="password"]', 'testpass123');
  await page.click('button[type="submit"]');
  await page.waitForURL('**/dashboard');
}

test.describe('Aggiunta manuale giocatore — US-018', () => {
  test('owner aggiunge un nuovo giocatore (email inesistente)', async ({ page }) => {
    const ownerEmail = uniqueEmail('addm_owner');
    const ownerToken = await registerUser(ownerEmail, 'Owner');
    await createCircle(ownerToken);

    await loginInBrowser(page, ownerEmail);
    await page.goto('http://localhost:4200/circles');

    await expect(page.locator('button', { hasText: '+ Giocatore' })).toBeVisible({ timeout: 10000 });
    await page.click('button:has-text("+ Giocatore")');
    await expect(page.locator('.invite-card')).toBeVisible({ timeout: 5000 });

    const newEmail = uniqueEmail('addm_new');
    await page.fill('input[type="email"]', newEmail);
    await page.click('button:has-text("Continua")');

    await expect(page.locator('text=Nessun account trovato')).toBeVisible({ timeout: 10000 });
    await page.fill('input[type="text"]', 'Nuovo Giocatore E2E');
    await page.click('button:has-text("Crea e aggiungi")');

    await expect(page.locator('text=Nuovo Giocatore E2E')).toBeVisible({ timeout: 10000 });
    await expect(page.locator('button:has-text("Chiudi")')).toBeVisible();
  });

  test('owner aggiunge un giocatore con email già registrata (conferma esplicita)', async ({ page }) => {
    const ownerEmail = uniqueEmail('addm_owner2');
    const existingEmail = uniqueEmail('addm_existing');
    const ownerToken = await registerUser(ownerEmail, 'Owner2');
    await registerUser(existingEmail, 'Giocatore Esistente');
    await createCircle(ownerToken);

    await loginInBrowser(page, ownerEmail);
    await page.goto('http://localhost:4200/circles');

    await page.click('button:has-text("+ Giocatore")');
    await expect(page.locator('.invite-card')).toBeVisible({ timeout: 5000 });

    await page.fill('input[type="email"]', existingEmail);
    await page.click('button:has-text("Continua")');

    await expect(page.locator('text=Giocatore Esistente')).toBeVisible({ timeout: 10000 });
    await page.click('button:has-text("Conferma e aggiungi")');

    await expect(page.locator('text=è stato aggiunto al circolo')).toBeVisible({ timeout: 10000 });
  });
});
