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

async function login(page: any, email: string): Promise<void> {
  await page.goto('/login');
  await page.fill('#email', email);
  await page.fill('#password', 'testpass123');
  await page.click('button[type="submit"]');
  await expect(page).toHaveURL(/dashboard/);
}

test.describe('MatchDetail — US-037', () => {
  test('partita confermata: dal click sulla data si apre il dettaglio con risultato, data, conferma e delta', async ({ page }) => {
    const t1 = uniqueEmail('m1'); const t1token = await registerUser(t1, 'Marco');
    const t2 = uniqueEmail('m2'); const t2token = await registerUser(t2, 'Luca');
    const t3 = uniqueEmail('m3'); const t3token = await registerUser(t3, 'Sara');
    const t4 = uniqueEmail('m4'); const t4token = await registerUser(t4, 'Giorgio');

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

    await login(page, t1);
    await page.goto(`/circles/${circleId}/matches`);
    // Il click che apre il dettaglio è sul link "Dettagli", non sulla data (non cliccabile).
    await page.click('.btn-detail');

    await expect(page).toHaveURL(new RegExp(`/circles/${circleId}/matches/${matchId}/detail`));
    await expect(page.locator('.status-badge--confirmed')).toBeVisible();
    await expect(page.locator('.score-hero')).toContainText('6-4');
    await expect(page.locator('.decision-strip')).toBeVisible();
    await expect(page.locator('.decision-title')).toContainText('Confermata da Giorgio');
    // .player-delta-row appare sia nella sezione "Conferme" (1 per giocatore) sia in "Variazione rating"
    // (1 per giocatore, solo se confermata): 4 + 4 = 8 righe totali.
    await expect(page.locator('.player-delta-row')).toHaveCount(8);
    await expect(page.locator('.section-heading', { hasText: 'Variazione rating' })).toBeVisible();
  });

  test('partita pending: il dettaglio non mostra delta né dati di conferma', async ({ page }) => {
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

    const matchId = await createMatch(t1token, circleId, [id1, id2], [id3, id4]);

    await login(page, t1);
    await page.goto(`/circles/${circleId}/matches/${matchId}/detail`);

    await expect(page.locator('.status-badge--pending')).toBeVisible();
    await expect(page.locator('.status-strip--pending')).toBeVisible();
    // La sezione "Conferme" mostra sempre una riga per giocatore (4), a prescindere dallo status;
    // solo la sezione "Variazione rating" (delta) è assente finché la partita non è confermata.
    await expect(page.locator('.player-delta-row')).toHaveCount(4);
    await expect(page.locator('.section-heading', { hasText: 'Variazione rating' })).toHaveCount(0);
    await expect(page.locator('.decision-strip')).toHaveCount(0);
  });

  test('utente non membro: accesso diretto al dettaglio viene negato', async ({ page }) => {
    const t1 = uniqueEmail('o1'); const t1token = await registerUser(t1, 'O1');
    const t2 = uniqueEmail('o2'); const t2token = await registerUser(t2, 'O2');
    const t3 = uniqueEmail('o3'); const t3token = await registerUser(t3, 'O3');
    const t4 = uniqueEmail('o4'); const t4token = await registerUser(t4, 'O4');
    const outsider = uniqueEmail('out'); await registerUser(outsider, 'Outsider');

    const circleId = await createCircleAndGetId(t1token);
    await joinCircle(t2token, circleId);
    await joinCircle(t3token, circleId);
    await joinCircle(t4token, circleId);

    const [id1, id2, id3, id4] = await Promise.all([
      decodeUserId(t1token), decodeUserId(t2token),
      decodeUserId(t3token), decodeUserId(t4token),
    ]);
    const matchId = await createMatch(t1token, circleId, [id1, id2], [id3, id4]);

    await login(page, outsider);
    await page.goto(`/circles/${circleId}/matches/${matchId}/detail`);

    await expect(page.locator('.form-error')).toBeVisible();
    await expect(page.locator('.player-delta-row')).toHaveCount(0);
  });
});
