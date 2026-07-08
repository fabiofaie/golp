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
  const r = await ctx.post(`${API}/circles`, { data: { name: `E2E_RC_${Date.now()}`, sport: 'padel' } });
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

test.describe('Configurazione metodo punteggio circolo — US-051', () => {
  test('owner cambia metodo a Game+Bonus, imposta N/M, salva e i valori persistono al reload', async ({ page }) => {
    const ownerEmail = uniqueEmail('rc_owner');
    const ownerToken = await registerUser(ownerEmail, 'Owner');
    const circleId = await createCircle(ownerToken);

    await loginInBrowser(page, ownerEmail);
    await page.goto('http://localhost:4200/circles');

    await expect(page.locator('button', { hasText: 'Punteggio' })).toBeVisible({ timeout: 10000 });
    await page.click('button:has-text("Punteggio")');

    await expect(page.locator('.rc-card')).toBeVisible({ timeout: 5000 });
    await page.click('.method-card:has-text("Game + Bonus")');

    await expect(page.locator('.window-params')).toBeVisible();
    const inputs = page.locator('.window-params input[type="number"]');
    await inputs.nth(0).fill('20');
    await inputs.nth(1).fill('4');

    await page.click('button:has-text("Salva configurazione")');
    await expect(page.locator('text=✓ Salvato')).toBeVisible({ timeout: 5000 });
    await page.click('.close-btn');

    // Reload: la configurazione deve essere ripresentata già impostata
    await page.reload();
    await expect(page.locator('button', { hasText: 'Punteggio' })).toBeVisible({ timeout: 10000 });
    await page.click('button:has-text("Punteggio")');
    await expect(page.locator('.method-card.selected')).toContainText('Game + Bonus');
    await expect(page.locator('.window-params input[type="number"]').nth(0)).toHaveValue('20');
    await expect(page.locator('.window-params input[type="number"]').nth(1)).toHaveValue('4');
    await page.click('.close-btn');

    // Leaderboard mostra l'etichetta del metodo attivo
    await page.goto(`http://localhost:4200/circles/${circleId}/leaderboard`);
    await expect(page.locator('.rating-method-badge')).toContainText('Game+Bonus');
  });

  test('non-owner non vede il pulsante di configurazione punteggio', async ({ page }) => {
    const ownerEmail = uniqueEmail('rc_owner2');
    const memberEmail = uniqueEmail('rc_member');
    const ownerToken = await registerUser(ownerEmail, 'Owner2');
    const memberToken = await registerUser(memberEmail, 'Member');
    const circleId = await createCircle(ownerToken);

    const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${memberToken}` } });
    await ctx.post(`${API}/circles/${circleId}/join`);
    await ctx.dispose();

    await loginInBrowser(page, memberEmail);
    await page.goto('http://localhost:4200/circles');

    await expect(page.locator('text=Solo proprietario')).not.toBeVisible();
    await expect(page.locator('button', { hasText: 'Punteggio' })).not.toBeVisible();
  });
});
