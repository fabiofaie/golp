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

async function createCircle(token: string, name: string): Promise<string> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${token}` } });
  const r = await ctx.post(`${API}/circles`, { data: { name, sport: 'padel' } });
  const body = await r.json();
  await ctx.dispose();
  return body.id as string;
}

async function loginAndGoToDashboard(page: import('@playwright/test').Page, email: string): Promise<void> {
  await page.goto('/login');
  await page.fill('#email', email);
  await page.fill('#password', 'testpass123');
  await page.click('button[type="submit"]');
  await expect(page).toHaveURL(/dashboard/);
}

test.describe('US-069 — Stati vuoti della dashboard per utenti nuovi', () => {
  test('AC1 — utente senza circoli vede il messaggio con CTA per crearne/unirsi', async ({ page }) => {
    const user = await registerUser(uniqueEmail('empty1'), 'Empty1');
    await loginAndGoToDashboard(page, user.email);

    await expect(page.locator('.auth-title')).toContainText('Ciao!');
    await expect(page.locator('a[routerLink="/circles/create"], a[href="/circles/create"]')).toBeVisible();
    await expect(page.locator('a[routerLink="/circles/browse"], a[href="/circles/browse"]')).toBeVisible();
  });

  test('AC5 — circolo con meno di 4 membri invita a completare il circolo, non a registrare', async ({ page }) => {
    const owner = await registerUser(uniqueEmail('empty2'), 'Empty2');
    await createCircle(owner.token, `E2E_US069_Low_${Date.now()}`);

    await loginAndGoToDashboard(page, owner.email);

    const emptyState = page.locator('.empty-state');
    await expect(emptyState).toContainText('Il circolo ha bisogno di altri giocatori');
    await expect(emptyState).not.toContainText('Registra la prima partita');
  });

  test('AC2/AC3 — circolo con 4 membri, nessuna partita/urgente: invito a registrare, nessuna sezione urgenti', async ({ page }) => {
    const owner = await registerUser(uniqueEmail('empty3'), 'Empty3');
    const p2 = await registerUser(uniqueEmail('empty3p2'), 'Empty3p2');
    const p3 = await registerUser(uniqueEmail('empty3p3'), 'Empty3p3');
    const p4 = await registerUser(uniqueEmail('empty3p4'), 'Empty3p4');

    const circleId = await createCircle(owner.token, `E2E_US069_Full_${Date.now()}`);
    for (const p of [p2, p3, p4]) {
      const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${p.token}` } });
      await ctx.post(`${API}/circles/${circleId}/join`);
      await ctx.dispose();
    }

    await loginAndGoToDashboard(page, owner.email);

    const emptyState = page.locator('.empty-state');
    await expect(emptyState).toContainText('Nessuna partita ancora');
    await expect(emptyState).not.toContainText('bisogno di altri giocatori');

    await expect(page.locator('.attention')).toHaveCount(0);

    // AC4 — nessun elemento rotto
    const bodyText = await page.locator('main.dashboard-content').innerText();
    expect(bodyText).not.toContain('undefined');
    expect(bodyText).not.toContain('null');
    expect(bodyText).not.toMatch(/\bNaN\b/);
  });
});
