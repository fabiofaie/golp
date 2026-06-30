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

async function createCircleAndGetId(token: string): Promise<string> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${token}` } });
  const r = await ctx.post(`${API}/circles`, { data: { name: `E2E_${Date.now()}`, sport: 'padel' } });
  const body = await r.json();
  await ctx.dispose();
  return body.id as string;
}

async function joinCircle(token: string, circleId: string): Promise<void> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${token}` } });
  await ctx.post(`${API}/circles/${circleId}/join`);
  await ctx.dispose();
}

async function decodeUserId(token: string): Promise<string> {
  const payload = JSON.parse(Buffer.from(token.split('.')[1], 'base64').toString());
  return payload.sub as string;
}

async function createMatch(token: string, circleId: string, t1: string[], t2: string[]): Promise<string> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${token}` } });
  const r = await ctx.post(`${API}/circles/${circleId}/matches`, {
    data: { team1: t1.map(id => ({ userId: id })), team2: t2.map(id => ({ userId: id })), sets: [{ team1: 6, team2: 4 }] },
  });
  const body = await r.json();
  await ctx.dispose();
  return body.id as string;
}

async function confirmMatch(token: string, circleId: string, matchId: string): Promise<void> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${token}` } });
  await ctx.post(`${API}/circles/${circleId}/matches/${matchId}/confirm`);
  await ctx.dispose();
}

test.describe('CircleMatchHistory — US-009', () => {
  test('navigating to match list shows match cards with status badges', async ({ page }) => {
    const t1 = uniqueEmail('m1'); const t1token = await registerUser(t1, 'M1');
    const t2 = uniqueEmail('m2'); const t2token = await registerUser(t2, 'M2');
    const t3 = uniqueEmail('m3'); const t3token = await registerUser(t3, 'M3');
    const t4 = uniqueEmail('m4'); const t4token = await registerUser(t4, 'M4');

    const circleId = await createCircleAndGetId(t1token);
    await joinCircle(t2token, circleId);
    await joinCircle(t3token, circleId);
    await joinCircle(t4token, circleId);

    const [id1, id2, id3, id4] = await Promise.all([
      decodeUserId(t1token), decodeUserId(t2token),
      decodeUserId(t3token), decodeUserId(t4token),
    ]);
    await createMatch(t1token, circleId, [id1, id2], [id3, id4]);

    await page.goto('/login');
    await page.fill('#email', t1);
    await page.fill('#password', 'testpass123');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/dashboard/);

    await page.goto(`/circles/${circleId}/matches`);
    await expect(page.locator('.match-card').first()).toBeVisible();
    await expect(page.locator('.status-badge').first()).toBeVisible();
  });

  test('confirmed match shows delta badge with +N pt or −N pt', async ({ page }) => {
    const t1 = uniqueEmail('d1'); const t1token = await registerUser(t1, 'D1');
    const t2 = uniqueEmail('d2'); const t2token = await registerUser(t2, 'D2');
    const t3 = uniqueEmail('d3'); const t3token = await registerUser(t3, 'D3');
    const t4 = uniqueEmail('d4'); const t4token = await registerUser(t4, 'D4');

    const circleId = await createCircleAndGetId(t1token);
    await joinCircle(t2token, circleId);
    await joinCircle(t3token, circleId);
    await joinCircle(t4token, circleId);

    const [id1, id2, id3, id4] = await Promise.all([
      decodeUserId(t1token), decodeUserId(t2token),
      decodeUserId(t3token), decodeUserId(t4token),
    ]);

    const matchId = await createMatch(t1token, circleId, [id1, id2], [id3, id4]);
    await confirmMatch(t2token, circleId, matchId);
    await confirmMatch(t3token, circleId, matchId);
    await confirmMatch(t4token, circleId, matchId);

    // login as t1 (team1 winner → positive delta)
    await page.goto('/login');
    await page.fill('#email', t1);
    await page.fill('#password', 'testpass123');
    await page.click('button[type="submit"]');

    await page.goto(`/circles/${circleId}/matches`);
    const badge = page.locator('.delta-badge').first();
    await expect(badge).toBeVisible();
    const text = await badge.textContent();
    // Badge deve contenere "+ pt" o "- pt" (senza esporre la formula)
    expect(text).toMatch(/[+\-]\d+ pt/);
  });

  test('pending match shows no delta badge', async ({ page }) => {
    const t1 = uniqueEmail('p1'); const t1token = await registerUser(t1, 'P1');
    const t2 = uniqueEmail('p2'); const t2token = await registerUser(t2, 'P2');
    const t3 = uniqueEmail('p3'); const t3token = await registerUser(t3, 'P3');
    const t4 = uniqueEmail('p4'); const t4token = await registerUser(t4, 'P4');

    const circleId = await createCircleAndGetId(t1token);
    await joinCircle(t2token, circleId);
    await joinCircle(t3token, circleId);
    await joinCircle(t4token, circleId);

    const [id1, id2, id3, id4] = await Promise.all([
      decodeUserId(t1token), decodeUserId(t2token),
      decodeUserId(t3token), decodeUserId(t4token),
    ]);
    await createMatch(t1token, circleId, [id1, id2], [id3, id4]);

    await page.goto('/login');
    await page.fill('#email', t1);
    await page.fill('#password', 'testpass123');
    await page.click('button[type="submit"]');

    await page.goto(`/circles/${circleId}/matches`);
    // partita pending: nessun .delta-badge
    await expect(page.locator('.delta-badge')).toHaveCount(0);
  });
});

test.describe('CircleMatchHistory — US-036 force-confirm warning', () => {
  test('clicking "Forza conferma" shows irreversibility warning without calling API', async ({ page }) => {
    const t1 = uniqueEmail('fc1'); const t1token = await registerUser(t1, 'FC1');
    const t2 = uniqueEmail('fc2'); const t2token = await registerUser(t2, 'FC2');
    const t3 = uniqueEmail('fc3'); const t3token = await registerUser(t3, 'FC3');
    const t4 = uniqueEmail('fc4'); const t4token = await registerUser(t4, 'FC4');

    const circleId = await createCircleAndGetId(t1token);
    await joinCircle(t2token, circleId);
    await joinCircle(t3token, circleId);
    await joinCircle(t4token, circleId);

    const [id1, id2, id3, id4] = await Promise.all([
      decodeUserId(t1token), decodeUserId(t2token),
      decodeUserId(t3token), decodeUserId(t4token),
    ]);
    await createMatch(t1token, circleId, [id1, id2], [id3, id4]);

    await page.goto('/login');
    await page.fill('#email', t1);
    await page.fill('#password', 'testpass123');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/dashboard/);

    await page.goto(`/circles/${circleId}/matches`);
    await expect(page.locator('.btn-force-confirm')).toBeVisible();

    await page.click('.btn-force-confirm');

    await expect(page.locator('.force-confirm-warning')).toBeVisible();
    await expect(page.locator('.force-confirm-warning')).toContainText('irreversibile');
    await expect(page.locator('.btn-force-confirm')).not.toBeVisible();
  });

  test('"Annulla" closes warning and match stays pending', async ({ page }) => {
    const t1 = uniqueEmail('fa1'); const t1token = await registerUser(t1, 'FA1');
    const t2 = uniqueEmail('fa2'); const t2token = await registerUser(t2, 'FA2');
    const t3 = uniqueEmail('fa3'); const t3token = await registerUser(t3, 'FA3');
    const t4 = uniqueEmail('fa4'); const t4token = await registerUser(t4, 'FA4');

    const circleId = await createCircleAndGetId(t1token);
    await joinCircle(t2token, circleId);
    await joinCircle(t3token, circleId);
    await joinCircle(t4token, circleId);

    const [id1, id2, id3, id4] = await Promise.all([
      decodeUserId(t1token), decodeUserId(t2token),
      decodeUserId(t3token), decodeUserId(t4token),
    ]);
    await createMatch(t1token, circleId, [id1, id2], [id3, id4]);

    await page.goto('/login');
    await page.fill('#email', t1);
    await page.fill('#password', 'testpass123');
    await page.click('button[type="submit"]');

    await page.goto(`/circles/${circleId}/matches`);
    await page.click('.btn-force-confirm');
    await expect(page.locator('.force-confirm-warning')).toBeVisible();

    await page.click('.btn-cancel-force');

    await expect(page.locator('.force-confirm-warning')).not.toBeVisible();
    await expect(page.locator('.btn-force-confirm')).toBeVisible();
    await expect(page.locator('.status-badge--pending')).toBeVisible();
  });

  test('"Confermo" button force-confirms match and shows it as confirmed', async ({ page }) => {
    const t1 = uniqueEmail('ff1'); const t1token = await registerUser(t1, 'FF1');
    const t2 = uniqueEmail('ff2'); const t2token = await registerUser(t2, 'FF2');
    const t3 = uniqueEmail('ff3'); const t3token = await registerUser(t3, 'FF3');
    const t4 = uniqueEmail('ff4'); const t4token = await registerUser(t4, 'FF4');

    const circleId = await createCircleAndGetId(t1token);
    await joinCircle(t2token, circleId);
    await joinCircle(t3token, circleId);
    await joinCircle(t4token, circleId);

    const [id1, id2, id3, id4] = await Promise.all([
      decodeUserId(t1token), decodeUserId(t2token),
      decodeUserId(t3token), decodeUserId(t4token),
    ]);
    await createMatch(t1token, circleId, [id1, id2], [id3, id4]);

    await page.goto('/login');
    await page.fill('#email', t1);
    await page.fill('#password', 'testpass123');
    await page.click('button[type="submit"]');

    await page.goto(`/circles/${circleId}/matches`);
    await page.click('.btn-force-confirm');
    await expect(page.locator('.force-confirm-warning')).toBeVisible();

    await page.click('.btn-confirm-force');

    await expect(page.locator('.force-confirm-warning')).not.toBeVisible();
    await expect(page.locator('.match-card--confirmed')).toBeVisible();
    await expect(page.locator('.status-badge--pending')).not.toBeVisible();
  });
});

test.describe('CircleMatchHistory — US-005', () => {
  test('pending match shows Conferma and Contesta buttons for current user', async ({ page }) => {
    const t1 = uniqueEmail('t1'); const t1token = await registerUser(t1, 'T1P1');
    const t2 = uniqueEmail('t2'); const t2token = await registerUser(t2, 'T1P2');
    const t3 = uniqueEmail('t3'); const t3token = await registerUser(t3, 'T2P1');
    const t4 = uniqueEmail('t4'); const t4token = await registerUser(t4, 'T2P2');

    const circleId = await createCircleAndGetId(t1token);
    await joinCircle(t2token, circleId);
    await joinCircle(t3token, circleId);
    await joinCircle(t4token, circleId);

    const [id1, id2, id3, id4] = await Promise.all([
      decodeUserId(t1token), decodeUserId(t2token),
      decodeUserId(t3token), decodeUserId(t4token),
    ]);

    await createMatch(t1token, circleId, [id1, id2], [id3, id4]);

    // login as t2 (has not confirmed yet)
    await page.goto('/login');
    await page.fill('#email', t2);
    await page.fill('#password', 'testpass123');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/dashboard/);

    await page.goto(`/circles/${circleId}/matches`);
    await expect(page.locator('.btn-confirm')).toBeVisible();
    await expect(page.locator('.btn-dispute')).toBeVisible();
  });

  test('clicking Conferma marks user as confirmed and shows "Hai già confermato"', async ({ page }) => {
    const t1 = uniqueEmail('c1'); const t1token = await registerUser(t1, 'C1');
    const t2 = uniqueEmail('c2'); const t2token = await registerUser(t2, 'C2');
    const t3 = uniqueEmail('c3'); const t3token = await registerUser(t3, 'C3');
    const t4 = uniqueEmail('c4'); const t4token = await registerUser(t4, 'C4');

    const circleId = await createCircleAndGetId(t1token);
    await joinCircle(t2token, circleId);
    await joinCircle(t3token, circleId);
    await joinCircle(t4token, circleId);

    const [id1, id2, id3, id4] = await Promise.all([
      decodeUserId(t1token), decodeUserId(t2token),
      decodeUserId(t3token), decodeUserId(t4token),
    ]);

    await createMatch(t1token, circleId, [id1, id2], [id3, id4]);

    await page.goto('/login');
    await page.fill('#email', t2);
    await page.fill('#password', 'testpass123');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/dashboard/);

    await page.goto(`/circles/${circleId}/matches`);
    await page.click('.btn-confirm');
    await expect(page.locator('.btn-confirm')).not.toBeVisible();
    await expect(page.locator('text=Hai già confermato')).toBeVisible();
  });

  test('clicking Contesta marks match as Contestata', async ({ page }) => {
    const t1 = uniqueEmail('d1'); const t1token = await registerUser(t1, 'D1');
    const t2 = uniqueEmail('d2'); const t2token = await registerUser(t2, 'D2');
    const t3 = uniqueEmail('d3'); const t3token = await registerUser(t3, 'D3');
    const t4 = uniqueEmail('d4'); const t4token = await registerUser(t4, 'D4');

    const circleId = await createCircleAndGetId(t1token);
    await joinCircle(t2token, circleId);
    await joinCircle(t3token, circleId);
    await joinCircle(t4token, circleId);

    const [id1, id2, id3, id4] = await Promise.all([
      decodeUserId(t1token), decodeUserId(t2token),
      decodeUserId(t3token), decodeUserId(t4token),
    ]);

    await createMatch(t1token, circleId, [id1, id2], [id3, id4]);

    await page.goto('/login');
    await page.fill('#email', t2);
    await page.fill('#password', 'testpass123');
    await page.click('button[type="submit"]');

    await page.goto(`/circles/${circleId}/matches`);
    await page.click('.btn-dispute');
    await expect(page.locator('text=Contestata')).toBeVisible();
    await expect(page.locator('.btn-confirm')).not.toBeVisible();
  });

  test('4/4 confirmations shows confirmed badge', async ({ page }) => {
    const t1 = uniqueEmail('f1'); const t1token = await registerUser(t1, 'F1');
    const t2 = uniqueEmail('f2'); const t2token = await registerUser(t2, 'F2');
    const t3 = uniqueEmail('f3'); const t3token = await registerUser(t3, 'F3');
    const t4 = uniqueEmail('f4'); const t4token = await registerUser(t4, 'F4');

    const circleId = await createCircleAndGetId(t1token);
    await joinCircle(t2token, circleId);
    await joinCircle(t3token, circleId);
    await joinCircle(t4token, circleId);

    const [id1, id2, id3, id4] = await Promise.all([
      decodeUserId(t1token), decodeUserId(t2token),
      decodeUserId(t3token), decodeUserId(t4token),
    ]);

    const matchId = await createMatch(t1token, circleId, [id1, id2], [id3, id4]);
    await confirmMatch(t2token, circleId, matchId);
    await confirmMatch(t3token, circleId, matchId);
    await confirmMatch(t4token, circleId, matchId);

    await page.goto('/login');
    await page.fill('#email', t1);
    await page.fill('#password', 'testpass123');
    await page.click('button[type="submit"]');

    await page.goto(`/circles/${circleId}/matches`);
    await expect(page.locator('text=Confermata')).toBeVisible();
    await expect(page.locator('.btn-confirm')).not.toBeVisible();
  });
});
