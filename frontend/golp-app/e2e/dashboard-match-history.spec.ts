import { test, expect, request as playwrightRequest } from '@playwright/test';

const API = 'http://localhost:5120';
const uniqueEmail = (prefix = 'u') => `${prefix}_${Date.now()}_${Math.random().toString(36).slice(2)}@e2e.test`;

async function registerUser(email: string, name: string): Promise<{ token: string; userId: string }> {
  const ctx = await playwrightRequest.newContext();
  const r = await ctx.post(`${API}/auth/register`, { data: { email, password: 'testpass123', name } });
  const body = await r.json();
  await ctx.dispose();
  return { token: body.token, userId: extractUserId(body.token) };
}

async function createCircle(token: string, sport = 'padel'): Promise<string> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${token}` } });
  const r = await ctx.post(`${API}/circles`, { data: { name: `E2E_History_${Date.now()}`, sport } });
  const body = await r.json();
  await ctx.dispose();
  return body.id as string;
}

async function joinCircle(token: string, circleId: string): Promise<void> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${token}` } });
  await ctx.post(`${API}/circles/${circleId}/join`);
  await ctx.dispose();
}

async function createMatch(token: string, circleId: string, t1p1: string, t1p2: string, t2p1: string, t2p2: string): Promise<string> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${token}` } });
  const r = await ctx.post(`${API}/circles/${circleId}/matches`, {
    data: {
      team1: [{ userId: t1p1 }, { userId: t1p2 }],
      team2: [{ userId: t2p1 }, { userId: t2p2 }],
      sets: [{ team1: 6, team2: 4 }],
    }
  });
  const body = await r.json();
  await ctx.dispose();
  return body.id as string;
}

function extractUserId(jwt: string): string {
  const payload = jwt.split('.')[1];
  const padded = payload + '='.repeat((4 - payload.length % 4) % 4);
  const decoded = atob(padded.replace(/-/g, '+').replace(/_/g, '/'));
  return JSON.parse(decoded).sub as string;
}

async function loginAs(page: import('@playwright/test').Page, email: string): Promise<void> {
  await page.goto('/login');
  await page.fill('#email', email);
  await page.fill('#password', 'testpass123');
  await page.click('button[type="submit"]');
  await expect(page).toHaveURL(/dashboard/);
}

test.describe('US-044 — dashboard match history', () => {

  test('Scenario 1: utente senza partite vede empty state', async ({ page }) => {
    const email = uniqueEmail('empty');
    await registerUser(email, 'EmptyUser');

    await loginAs(page, email);
    await page.goto('/my-matches');

    await expect(page.getByText('Nessuna partita ancora')).toBeVisible();
  });

  test('Scenario 2: utente con partite vede lista con score e badge stato', async ({ page }) => {
    const email = uniqueEmail('owner');
    const { token, userId } = await registerUser(email, 'OwnerUser');

    const { token: p2Token, userId: p2Id } = await registerUser(uniqueEmail('p2'), 'Player2');
    const { token: p3Token, userId: p3Id } = await registerUser(uniqueEmail('p3'), 'Player3');
    const { token: p4Token, userId: p4Id } = await registerUser(uniqueEmail('p4'), 'Player4');

    const circleId = await createCircle(token);
    await joinCircle(p2Token, circleId);
    await joinCircle(p3Token, circleId);
    await joinCircle(p4Token, circleId);

    await createMatch(token, circleId, userId, p2Id, p3Id, p4Id);

    await loginAs(page, email);
    await page.goto('/my-matches');

    await expect(page.locator('.match-row').first()).toBeVisible();
    // Score presente (6–4)
    await expect(page.getByText('6–4')).toBeVisible();
    // Status badge presente
    await expect(page.locator('.status-pip').first()).toBeVisible();
  });

  test('Scenario 3: toggle "In attesa" mostra solo partite pending', async ({ page }) => {
    const email = uniqueEmail('filter');
    const { token, userId } = await registerUser(email, 'FilterUser');

    const { token: p2Token, userId: p2Id } = await registerUser(uniqueEmail('p2'), 'Player2');
    const { token: p3Token, userId: p3Id } = await registerUser(uniqueEmail('p3'), 'Player3');
    const { token: p4Token, userId: p4Id } = await registerUser(uniqueEmail('p4'), 'Player4');

    const circleId = await createCircle(token);
    await joinCircle(p2Token, circleId);
    await joinCircle(p3Token, circleId);
    await joinCircle(p4Token, circleId);

    // Crea una partita pending
    await createMatch(token, circleId, userId, p2Id, p3Id, p4Id);

    await loginAs(page, email);
    await page.goto('/my-matches');

    // Click toggle "In attesa"
    await page.getByRole('button', { name: 'In attesa' }).click();

    // Almeno una riga visibile con status pending
    await expect(page.locator('.status-pip.status-pending').first()).toBeVisible();

    // Nessun badge "Confermata"
    await expect(page.locator('.status-pip.status-confirmed')).toHaveCount(0);
  });

});
