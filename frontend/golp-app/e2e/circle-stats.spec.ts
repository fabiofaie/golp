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
  const r = await ctx.post(`${API}/circles`, { data: { name: `E2E_${Date.now()}`, sport: 'padel' } });
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

test.describe('CircleStats — US-011', () => {
  test('stats page loads and shows stat cards (with null values for new user)', async ({ page }) => {
    const email = uniqueEmail('st');
    const token = await registerUser(email, 'Tester Stats');
    const circleId = await createCircle(token);

    await loginInBrowser(page, email);
    await page.goto(`http://localhost:4200/circles/${circleId}/stats`);

    // New user has no matches → either empty state or stat cards with "Dati non sufficienti"
    // In both cases the page title should be visible and no error
    await expect(page.locator('h1')).toContainText('Statistiche', { timeout: 10000 });
    await expect(page.locator('.form-error')).not.toBeVisible();
  });

  test('shows empty state when no confirmed matches exist', async ({ page }) => {
    const email = uniqueEmail('st2');
    const token = await registerUser(email, 'Tester Empty Stats');
    const circleId = await createCircle(token);

    await loginInBrowser(page, email);
    await page.goto(`http://localhost:4200/circles/${circleId}/stats`);

    // With no matches at all → isEmpty true → empty state
    await expect(page.locator('text=Ancora nessuna statistica')).toBeVisible({ timeout: 10000 });
    await expect(page.locator('.stats-list')).not.toBeAttached();
  });

  test('stats page accessible via "Stats" link on my-circles', async ({ page }) => {
    const email = uniqueEmail('st3');
    const token = await registerUser(email, 'Tester Link Stats');
    const circleId = await createCircle(token);

    await loginInBrowser(page, email);
    await page.goto(`http://localhost:4200/circles`);

    await expect(page.locator('h1')).toContainText('miei circoli', { timeout: 10000 });

    const statsLink = page.locator(`a[href="/circles/${circleId}/stats"]`);
    await expect(statsLink).toBeVisible({ timeout: 5000 });
    await statsLink.click();

    await expect(page.locator('h1')).toContainText('Statistiche', { timeout: 10000 });
  });
});
