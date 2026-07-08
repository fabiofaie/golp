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
  const r = await ctx.post(`${API}/circles`, { data: { name: `E2E_GB_${Date.now()}`, sport: 'padel' } });
  const body = await r.json();
  await ctx.dispose();
  return body.id as string;
}

async function setGameBonusMethod(token: string, circleId: string): Promise<void> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${token}` } });
  await ctx.put(`${API}/circles/${circleId}/rating-config`, {
    data: { ratingMethod: 'GameBonus', gameBonusWindowMatches: 30, gameBonusWindowWeeks: 6 },
  });
  await ctx.dispose();
}

async function loginInBrowser(page: import('@playwright/test').Page, email: string): Promise<void> {
  await page.goto('http://localhost:4200/login');
  await page.fill('input[type="email"]', email);
  await page.fill('input[type="password"]', 'testpass123');
  await page.click('button[type="submit"]');
  await page.waitForURL('**/dashboard');
}

test.describe('Pagina spiegazione Game+Bonus e simulatore — US-055', () => {
  test('link rating dalla leaderboard di un circolo Game+Bonus porta a /game-bonus-info e il simulatore calcola i punti', async ({ page }) => {
    const ownerEmail = uniqueEmail('gb_owner');
    const ownerToken = await registerUser(ownerEmail, 'Owner');
    const circleId = await createCircle(ownerToken);
    await setGameBonusMethod(ownerToken, circleId);

    await loginInBrowser(page, ownerEmail);
    await page.goto(`http://localhost:4200/circles/${circleId}/leaderboard`);

    await expect(page.locator('.rating-method-badge')).toContainText('Game+Bonus');
    await page.click('.elo-info-link');
    await page.waitForURL('**/game-bonus-info');

    await expect(page.locator('h1.auth-title')).toContainText('Come funziona il punteggio');

    const scoreInputs = page.locator('.score-input');
    await scoreInputs.nth(0).fill('6');
    await scoreInputs.nth(1).fill('4');

    await page.click('button[type="submit"]');
    await expect(page.locator('.result-panel')).toBeVisible({ timeout: 5000 });
    await expect(page.locator('.result-row').first()).toContainText('+3');
  });

  test('pagina raggiungibile direttamente senza contesto circolo', async ({ page }) => {
    await page.goto('http://localhost:4200/game-bonus-info');
    await expect(page.locator('h1.auth-title')).toContainText('Come funziona il punteggio');
  });
});
