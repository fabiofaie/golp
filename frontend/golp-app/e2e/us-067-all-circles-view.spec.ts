import { test, expect, request as playwrightRequest } from '@playwright/test';

const API = 'http://localhost:5120';
const uniqueEmail = (prefix = 'u') => `${prefix}_${Date.now()}_${Math.random().toString(36).slice(2)}@e2e.test`;

async function registerUser(email: string, name: string): Promise<{ token: string; email: string }> {
  const ctx = await playwrightRequest.newContext();
  const r = await ctx.post(`${API}/auth/register`, { data: { email, password: 'testpass123', name } });
  const body = await r.json();
  await ctx.dispose();
  const token = body.accessToken ?? body.token;
  return { token, email };
}

async function createCircle(token: string, name: string, sport: string): Promise<string> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${token}` } });
  const r = await ctx.post(`${API}/circles`, { data: { name, sport } });
  const body = await r.json();
  await ctx.dispose();
  return body.id as string;
}

test.describe('US-067 — Vista aggregata "Tutti i circoli"', () => {
  test('selezionando "Tutti i circoli" mostra contatori e card per circolo, senza rating aggregato', async ({ page }) => {
    const owner = await registerUser(uniqueEmail('agg'), 'Owner Agg');
    const circleAName = `E2E_US067_A_${Date.now()}`;
    const circleBName = `E2E_US067_B_${Date.now()}`;
    await createCircle(owner.token, circleAName, 'padel');
    await createCircle(owner.token, circleBName, 'beachtennis');

    await page.goto('/login');
    await page.fill('#email', owner.email);
    await page.fill('#password', 'testpass123');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/dashboard/);

    await page.click('.circle-picker');
    await page.click('.circle-row:has-text("Tutti i circoli")');
    await expect(page.locator('.circle-sheet')).toHaveCount(0);

    // AC2 — contatori aggregati (nessuna partita confirmed seedata: 0 partite, 0% vittorie)
    const stats = page.locator('.aggregate-stat');
    await expect(stats).toHaveCount(4);
    await expect(stats.nth(0).locator('strong')).toHaveText('2'); // circoli attivi
    await expect(stats.nth(1).locator('strong')).toHaveText('0'); // partite giocate
    await expect(stats.nth(2).locator('strong')).toHaveText('0%'); // vittorie

    // AC3 — card sintetica per ciascun circolo
    const cards = page.locator('.circle-summary-card');
    await expect(cards).toHaveCount(2);
    await expect(cards.filter({ hasText: circleAName })).toBeVisible();
    await expect(cards.filter({ hasText: circleBName })).toBeVisible();

    // AC1 — nessun rating aggregato mostrato in nessun punto
    await expect(page.locator('.rating-main')).toHaveCount(0);
    const bodyText = await page.locator('.dashboard-content').innerText();
    expect(bodyText.toLowerCase()).not.toContain('rating totale');

    // AC5 — "+" in modalità "Tutti i circoli" non pre-seleziona un circolo
    await page.click('.bottom-nav .nav-action, a[href="/match/quick"]');
    await expect(page).toHaveURL(/\/match\/quick(\?|$)/);
    expect(page.url()).not.toContain('circleId=');
  });
});
