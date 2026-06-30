import { test, expect, request as playwrightRequest } from '@playwright/test';

const API = 'http://localhost:5120';
const uid = () => `lb_${Date.now()}_${Math.random().toString(36).slice(2)}`;

async function registerUser(name: string): Promise<{ token: string; userId: string; email: string }> {
  const ctx = await playwrightRequest.newContext();
  const email = `${uid()}@e2e.test`;
  const r = await ctx.post(`${API}/auth/register`, {
    data: { email, password: 'testpass123', name },
  });
  const body = await r.json();
  await ctx.dispose();
  const payload = JSON.parse(Buffer.from(body.token.split('.')[1], 'base64').toString());
  return { token: body.token as string, userId: payload.sub as string, email };
}

async function createCircle(token: string): Promise<string> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${token}` } });
  const r = await ctx.post(`${API}/circles`, { data: { name: `LB_E2E_${uid()}`, sport: 'padel' } });
  const body = await r.json();
  await ctx.dispose();
  return body.id as string;
}

async function joinCircle(token: string, circleId: string): Promise<void> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${token}` } });
  await ctx.post(`${API}/circles/${circleId}/join`);
  await ctx.dispose();
}

async function createAndConfirmMatch(
  tokens: string[], circleId: string,
  t1: string[], t2: string[],
): Promise<void> {
  const ctx0 = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${tokens[0]}` } });
  const r = await ctx0.post(`${API}/circles/${circleId}/matches`, {
    data: { team1: t1.map(id => ({ userId: id })), team2: t2.map(id => ({ userId: id })), sets: [{ team1: 6, team2: 4 }] },
  });
  const matchId = (await r.json()).id as string;
  await ctx0.dispose();

  for (let i = 1; i < tokens.length; i++) {
    const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${tokens[i]}` } });
    await ctx.post(`${API}/circles/${circleId}/matches/${matchId}/confirm`);
    await ctx.dispose();
  }
}

// Logs in via the UI form and navigates to the leaderboard using Angular's client-side router.
// We cannot use page.goto('/circles/...') because the dev server proxy intercepts /circles/**
// and forwards to the backend API, returning JSON instead of index.html.
// Instead: login → dashboard → click "I miei circoli" routerLink → click "Classifica" routerLink.
// Both clicks use Angular's client-side navigation, no full page reload, no proxy intercept.
async function loginAndNavigateToLeaderboard(
  page: Parameters<typeof test>[1],
  email: string,
  circleId: string,
): Promise<void> {
  await page.goto('/login');
  await page.fill('#email', email);
  await page.fill('#password', 'testpass123');
  await page.click('button[type="submit"]');
  await page.waitForURL(/dashboard/);

  // Client-side navigate to /circles (routerLink, no proxy, no full page reload)
  await page.click('a[href="/circles"]');

  // Wait for the specific circle's "Classifica" routerLink to appear (auto-waits for API response)
  await page.locator(`a[href*="${circleId}"][href*="leaderboard"]`).click();
  await page.waitForURL(`**/circles/${circleId}/leaderboard`);
}

test.describe('CircleLeaderboard — US-008', () => {

  test('shows leaderboard table after confirmed matches', async ({ page }) => {
    const [p1, p2, p3, p4] = await Promise.all([
      registerUser('Alpha'),
      registerUser('Beta'),
      registerUser('Gamma'),
      registerUser('Delta'),
    ]);
    const circleId = await createCircle(p1.token);
    await joinCircle(p2.token, circleId);
    await joinCircle(p3.token, circleId);
    await joinCircle(p4.token, circleId);

    await createAndConfirmMatch(
      [p1.token, p2.token, p3.token, p4.token],
      circleId,
      [p1.userId, p2.userId],
      [p3.userId, p4.userId],
    );

    await loginAndNavigateToLeaderboard(page, p1.email, circleId);
    await expect(page.locator('.leaderboard-row')).toHaveCount(4);
  });

  test('current user row has leaderboard-row--current class', async ({ page }) => {
    const [p1, p2, p3, p4] = await Promise.all([
      registerUser('Me'),
      registerUser('You'),
      registerUser('Him'),
      registerUser('Her'),
    ]);
    const circleId = await createCircle(p1.token);
    await joinCircle(p2.token, circleId);
    await joinCircle(p3.token, circleId);
    await joinCircle(p4.token, circleId);

    await createAndConfirmMatch(
      [p1.token, p2.token, p3.token, p4.token],
      circleId,
      [p1.userId, p2.userId],
      [p3.userId, p4.userId],
    );

    await loginAndNavigateToLeaderboard(page, p1.email, circleId);
    await expect(page.locator('.leaderboard-row--current')).toHaveCount(1);
  });

  test('unclassified section visible when some players have no confirmed matches', async ({ page }) => {
    const [p1, p2, p3, p4] = await Promise.all([
      registerUser('PlayA'),
      registerUser('PlayB'),
      registerUser('PlayC'),
      registerUser('PlayD'),
    ]);
    const circleId = await createCircle(p1.token);
    await joinCircle(p2.token, circleId);
    await joinCircle(p3.token, circleId);
    await joinCircle(p4.token, circleId);

    const p5 = await registerUser('Bench');
    await joinCircle(p5.token, circleId);

    await createAndConfirmMatch(
      [p1.token, p2.token, p3.token, p4.token],
      circleId,
      [p1.userId, p2.userId],
      [p3.userId, p4.userId],
    );

    await loginAndNavigateToLeaderboard(page, p1.email, circleId);
    await expect(page.locator('.unclassified-section')).toBeVisible();
    await expect(page.locator('.unclassified-row')).toHaveCount(1);
  });

  test('owner appears in unclassified when circle has no confirmed matches', async ({ page }) => {
    const p1 = await registerUser('Solo');
    const circleId = await createCircle(p1.token);

    await loginAndNavigateToLeaderboard(page, p1.email, circleId);
    // No confirmed matches → no classified rows, owner shown in unclassified
    await expect(page.locator('.leaderboard-row')).toHaveCount(0);
    await expect(page.locator('.unclassified-section')).toBeVisible();
  });

});
