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

async function decodeUserId(token: string): Promise<string> {
  const payload = JSON.parse(Buffer.from(token.split('.')[1], 'base64').toString());
  return payload.sub as string;
}

async function createCircle(token: string, name: string): Promise<string> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${token}` } });
  const r = await ctx.post(`${API}/circles`, { data: { name, sport: 'padel' } });
  const body = await r.json();
  await ctx.dispose();
  return body.id as string;
}

async function joinCircle(token: string, circleId: string): Promise<void> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${token}` } });
  await ctx.post(`${API}/circles/${circleId}/join`);
  await ctx.dispose();
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

async function disputeMatch(token: string, circleId: string, matchId: string): Promise<void> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${token}` } });
  await ctx.post(`${API}/circles/${circleId}/matches/${matchId}/dispute`);
  await ctx.dispose();
}

test.describe('US-068 — Trattamento visivo distinto conferme/contestazioni pendenti', () => {
  test('dashboard mostra pending e disputed contemporaneamente, distinte, con navigazione corretta', async ({ page }) => {
    const owner = await registerUser(uniqueEmail('u068'), 'Owner');
    const p2 = await registerUser(uniqueEmail('u068p2'), 'Player2');
    const p3 = await registerUser(uniqueEmail('u068p3'), 'Player3');
    const p4 = await registerUser(uniqueEmail('u068p4'), 'Player4');

    const circleId = await createCircle(owner.token, `E2E_US068_${Date.now()}`);
    await joinCircle(p2.token, circleId);
    await joinCircle(p3.token, circleId);
    await joinCircle(p4.token, circleId);

    const [ownerId, p2Id, p3Id, p4Id] = await Promise.all([
      decodeUserId(owner.token), decodeUserId(p2.token), decodeUserId(p3.token), decodeUserId(p4.token),
    ]);

    // Match A: p2 non conferma -> resta pending per p2
    await createMatch(owner.token, circleId, [ownerId, p2Id], [p3Id, p4Id]);

    // Match B: p2 la contesta esplicitamente -> disputed
    const matchB = await createMatch(owner.token, circleId, [ownerId, p3Id], [p2Id, p4Id]);
    await disputeMatch(p2.token, circleId, matchB);

    await page.goto('/login');
    await page.fill('#email', p2.email);
    await page.fill('#password', 'testpass123');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/dashboard/);

    // AC5 — entrambe le categorie visibili contemporaneamente
    const cards = page.locator('.pending-card');
    await expect(cards).toHaveCount(2);

    // AC1/AC2 — distinzione visiva
    const pendingCard = page.locator('.pending-card:not(.disputed)');
    const disputedCard = page.locator('.pending-card.disputed');
    await expect(pendingCard).toHaveCount(1);
    await expect(disputedCard).toHaveCount(1);
    await expect(pendingCard).toContainText('DA CONFERMARE');
    await expect(disputedCard).toContainText('CONTESTATA');

    // AC6 — badge conteggio somma entrambe
    await expect(page.locator('.attention .count')).toHaveText('2');

    // AC4 — click sulla disputed porta al dettaglio contestazione
    await disputedCard.locator('a.primary').click();
    await expect(page.locator('.status-badge--disputed')).toBeVisible();
    await expect(page.locator('.status-badge--disputed')).toContainText('Contestata');
    // distinta dal flusso di conferma: nessun bottone "conferma" per una partita già disputed
    await expect(page.locator('.btn-confirm-hero')).toHaveCount(0);
  });
});
