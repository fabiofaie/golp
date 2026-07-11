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

async function createCircle(token: string, name: string, sport = 'padel'): Promise<string> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${token}` } });
  const r = await ctx.post(`${API}/circles`, { data: { name, sport } });
  const body = await r.json();
  await ctx.dispose();
  return body.id as string;
}

test.describe('US-070 — Endpoint aggregato per il caricamento performante della dashboard', () => {
  test('il caricamento della dashboard (circolo singolo) effettua una sola chiamata a /dashboard/summary', async ({ page }) => {
    const owner = await registerUser(uniqueEmail('u070a'), 'Owner070a');
    await createCircle(owner.token, `E2E_US070_A_${Date.now()}`);

    let summaryCalls = 0;
    page.on('request', req => {
      if (req.url().includes('/dashboard/summary')) summaryCalls++;
    });

    await page.goto('/login');
    await page.fill('#email', owner.email);
    await page.fill('#password', 'testpass123');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/dashboard/);
    await page.waitForTimeout(500);

    expect(summaryCalls).toBe(1);
    await expect(page.locator('.rating-main')).toBeVisible();
  });

  test('AC3 — il numero di chiamate a /dashboard/summary non aumenta con più circoli (utente con 3 circoli)', async ({ page }) => {
    const owner = await registerUser(uniqueEmail('u070b'), 'Owner070b');
    await createCircle(owner.token, `E2E_US070_B1_${Date.now()}`, 'padel');
    await createCircle(owner.token, `E2E_US070_B2_${Date.now()}`, 'beachtennis');
    await createCircle(owner.token, `E2E_US070_B3_${Date.now()}`, 'basket2v2');

    let summaryCalls = 0;
    page.on('request', req => {
      if (req.url().includes('/dashboard/summary')) summaryCalls++;
    });

    await page.goto('/login');
    await page.fill('#email', owner.email);
    await page.fill('#password', 'testpass123');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/dashboard/);
    await page.waitForTimeout(500);

    expect(summaryCalls).toBe(1);
  });

  test('modalità "Tutti i circoli" effettua anch\'essa una sola chiamata a /dashboard/summary', async ({ page }) => {
    const owner = await registerUser(uniqueEmail('u070c'), 'Owner070c');
    const nameA = `E2E_US070_C1_${Date.now()}`;
    const nameB = `E2E_US070_C2_${Date.now()}`;
    await createCircle(owner.token, nameA, 'padel');
    await createCircle(owner.token, nameB, 'beachtennis');

    await page.goto('/login');
    await page.fill('#email', owner.email);
    await page.fill('#password', 'testpass123');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/dashboard/);
    await page.waitForTimeout(300);

    let summaryCallsAfterSwitch = 0;
    page.on('request', req => {
      if (req.url().includes('/dashboard/summary')) summaryCallsAfterSwitch++;
    });

    await page.click('.circle-picker');
    await page.click('.circle-row:has-text("Tutti i circoli")');
    await page.waitForTimeout(500);

    expect(summaryCallsAfterSwitch).toBe(1);
    await expect(page.locator('.aggregate-stat')).toHaveCount(4);
  });
});
