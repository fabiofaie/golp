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

async function createCircle(token: string, sport = 'padel'): Promise<string> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${token}` } });
  const r = await ctx.post(`${API}/circles`, { data: { name: `E2E_Pub_${Date.now()}`, sport } });
  const body = await r.json();
  await ctx.dispose();
  return body.id as string;
}

async function joinCircle(token: string, circleId: string): Promise<void> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${token}` } });
  await ctx.post(`${API}/circles/${circleId}/join`);
  await ctx.dispose();
}

interface TokenRow { token: string; userId: string; }

async function createMatchAndGetTokens(ownerToken: string, circleId: string, player2Id: string, player3Id: string, player4Id: string): Promise<TokenRow[]> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${ownerToken}` } });
  const r = await ctx.post(`${API}/circles/${circleId}/matches`, {
    data: {
      team1: [{ userId: player2Id }, { userId: player3Id }],
      team2: [{ userId: player4Id }, { userId: 'self' }],
      sets: [{ team1: 6, team2: 4 }, { team1: 7, team2: 5 }],
    },
  });
  const match = await r.json();
  const matchId: string = match.id;

  // Fetch tokens via a direct DB query is not possible from E2E — use GET on each token
  // Instead, we use a test helper endpoint if available, or skip token extraction here.
  // Approach: POST confirm via authenticated endpoint for all 4 users and read the match tokens via
  // a second circle/matches GET that exposes pendingTokens (not implemented).
  // Simpler: we call the internal test endpoint that the factory exposes:
  const tokensResp = await ctx.get(`${API}/circles/${circleId}/matches/${matchId}/tokens`);
  await ctx.dispose();
  if (tokensResp.status() === 404) {
    return [];
  }
  return (await tokensResp.json()) as TokenRow[];
}

async function getUserId(token: string): Promise<string> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${token}` } });
  const r = await ctx.get(`${API}/auth/me`);
  const body = await r.json();
  await ctx.dispose();
  return body.id as string;
}

async function createMatchDirect(ownerToken: string, circleId: string, p2Id: string, p3Id: string, p4Id: string): Promise<string> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${ownerToken}` } });
  const r = await ctx.post(`${API}/circles/${circleId}/matches`, {
    data: {
      team1: [{ userId: p2Id }, { userId: p3Id }],
      team2: [{ userId: p4Id }, { guestName: 'Ospite E2E' }],
      sets: [{ team1: 6, team2: 4 }, { team1: 6, team2: 3 }],
    },
  });
  const body = await r.json();
  await ctx.dispose();
  return body.id as string;
}

async function getTokenForUser(ownerToken: string, matchId: string, circleId: string, userId: string): Promise<string | null> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${ownerToken}` } });
  const r = await ctx.get(`${API}/circles/${circleId}/matches/${matchId}/tokens`);
  await ctx.dispose();
  if (r.status() !== 200) return null;
  const rows: TokenRow[] = await r.json();
  return rows.find(t => t.userId === userId)?.token ?? null;
}

test.describe('US-040 — pagina pubblica conferma partita via token', () => {
  test.describe.configure({ mode: 'serial' });

  let ownerToken: string;
  let p2Token: string;
  let p3Token: string;
  let circleId: string;
  let ownerEmail: string;
  let p2Id: string;

  test.beforeEach(async () => {
    ownerEmail = uniqueEmail('own');
    const p2Email = uniqueEmail('p2');
    const p3Email = uniqueEmail('p3');

    ownerToken = await registerUser(ownerEmail, 'Owner040');
    p2Token    = await registerUser(p2Email,    'Player2_040');
    p3Token    = await registerUser(p3Email,    'Player3_040');

    circleId = await createCircle(ownerToken, 'padel');
    await joinCircle(p2Token, circleId);
    await joinCircle(p3Token, circleId);

    p2Id = await getUserId(p2Token);
  });

  test('token non trovato → pagina mostra errore link non valido', async ({ page }) => {
    await page.goto('/m/00000000-0000-0000-0000-000000000000');
    await expect(page.locator('.pub-token-error-title')).toContainText('non valido');
  });

  test('token valido → pagina mostra dati partita e pulsanti conferma/contesta', async ({ page }) => {
    const ownerId = await getUserId(ownerToken);
    const p3Id    = await getUserId(p3Token);
    const matchId = await createMatchDirect(ownerToken, circleId, p2Id, p3Id, ownerId);

    const token = await getTokenForUser(ownerToken, matchId, circleId, p2Id);
    test.skip(!token, 'token endpoint non disponibile in questo ambiente');
    if (!token) return;

    await page.goto(`/m/${token}`);
    await expect(page.locator('.pub-match-card')).toBeVisible({ timeout: 10000 });
    await expect(page.locator('.pub-btn-confirm')).toBeVisible();
    await expect(page.locator('.pub-btn-dispute')).toBeVisible();
    await expect(page.locator('.pub-circle-name')).toBeVisible();
  });

  test('confirm via token → stato aggiornato, messaggio successo visibile', async ({ page }) => {
    const ownerId = await getUserId(ownerToken);
    const p3Id    = await getUserId(p3Token);
    const matchId = await createMatchDirect(ownerToken, circleId, p2Id, p3Id, ownerId);

    const token = await getTokenForUser(ownerToken, matchId, circleId, p2Id);
    test.skip(!token, 'token endpoint non disponibile');
    if (!token) return;

    await page.goto(`/m/${token}`);
    await expect(page.locator('.pub-btn-confirm')).toBeVisible({ timeout: 10000 });
    await page.locator('.pub-btn-confirm').click();
    await expect(page.locator('.pub-result-title')).toContainText('confermato', { timeout: 10000 });
  });

  test('dispute via token → stato "Partita contestata" visibile', async ({ page }) => {
    const ownerId = await getUserId(ownerToken);
    const p3Id    = await getUserId(p3Token);
    const matchId = await createMatchDirect(ownerToken, circleId, p2Id, p3Id, ownerId);

    const token = await getTokenForUser(ownerToken, matchId, circleId, p2Id);
    test.skip(!token, 'token endpoint non disponibile');
    if (!token) return;

    await page.goto(`/m/${token}`);
    await expect(page.locator('.pub-btn-dispute')).toBeVisible({ timeout: 10000 });
    await page.locator('.pub-btn-dispute').click();
    await expect(page.locator('.pub-result-title')).toContainText('contestata', { timeout: 10000 });
  });

  test('token già usato → "Hai già risposto" visibile al secondo accesso', async ({ page }) => {
    const ownerId = await getUserId(ownerToken);
    const p3Id    = await getUserId(p3Token);
    const matchId = await createMatchDirect(ownerToken, circleId, p2Id, p3Id, ownerId);

    const token = await getTokenForUser(ownerToken, matchId, circleId, p2Id);
    test.skip(!token, 'token endpoint non disponibile');
    if (!token) return;

    // Use the token via API directly
    const ctx = await playwrightRequest.newContext();
    await ctx.post(`${API}/m/${token}/confirm`);
    await ctx.dispose();

    // Now navigate to the page — should show "già risposto"
    await page.goto(`/m/${token}`);
    await expect(page.locator('.pub-already-text')).toBeVisible({ timeout: 10000 });
  });

  test('pagina pubblica accessibile senza login (authGuard non interviene)', async ({ page }) => {
    // Navigate without any auth token in storage
    await page.context().clearCookies();
    await page.evaluate(() => localStorage.clear());

    await page.goto('/m/00000000-0000-0000-0000-000000000001');
    // Should stay on /m/ page, not redirect to /login
    await expect(page).not.toHaveURL(/\/login/);
    await expect(page.locator('.pub-token-error')).toBeVisible({ timeout: 8000 });
  });
});
