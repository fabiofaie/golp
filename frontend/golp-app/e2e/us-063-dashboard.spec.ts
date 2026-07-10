import { test, expect, request as playwrightRequest } from '@playwright/test';

const API = 'http://localhost:5120';
const uniqueEmail = (prefix = 'u') => `${prefix}_${Date.now()}_${Math.random().toString(36).slice(2)}@e2e.test`;

function decodeUserId(jwt: string): string {
  const payload = JSON.parse(Buffer.from(jwt.split('.')[1], 'base64').toString('utf-8'));
  return payload.sub;
}

async function registerUser(email: string, name: string): Promise<{ token: string; id: string; email: string }> {
  const ctx = await playwrightRequest.newContext();
  const r = await ctx.post(`${API}/auth/register`, { data: { email, password: 'testpass123', name } });
  const body = await r.json();
  await ctx.dispose();
  const token = body.accessToken ?? body.token;
  return { token, id: decodeUserId(token), email };
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

async function createMatch(
  token: string, circleId: string,
  team1: string[], team2: string[], sets: { team1: number; team2: number }[],
): Promise<string> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${token}` } });
  const r = await ctx.post(`${API}/circles/${circleId}/matches`, {
    data: {
      team1: team1.map(userId => ({ userId })),
      team2: team2.map(userId => ({ userId })),
      sets,
    },
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

test.describe('US-063 — Redesign dashboard', () => {
  test('circolo attivo, azioni urgenti, ultime partite e guardia sui 4 membri', async ({ page }) => {
    const owner = await registerUser(uniqueEmail('own'), 'Owner');
    const p2 = await registerUser(uniqueEmail('p2'), 'Player2');
    const p3 = await registerUser(uniqueEmail('p3'), 'Player3');
    const p4 = await registerUser(uniqueEmail('p4'), 'Player4');

    // Circolo A: pieno (4 membri), diventerà il circolo attivo (iscrizione più vecchia)
    const circleA = await createCircle(owner.token, `E2E_US063_A_${Date.now()}`);
    await joinCircle(p2.token, circleA);
    await joinCircle(p3.token, circleA);
    await joinCircle(p4.token, circleA);

    // Circolo B: sotto i 4 membri, creato dopo A (non deve diventare il circolo attivo di default)
    const circleB = await createCircle(owner.token, `E2E_US063_B_${Date.now()}`);
    await joinCircle(p2.token, circleB);

    // Partita confirmed nel circolo attivo (owner vince)
    const wonMatchId = await createMatch(
      owner.token, circleA,
      [owner.id, p2.id], [p3.id, p4.id],
      [{ team1: 6, team2: 3 }, { team1: 6, team2: 4 }],
    );
    await confirmMatch(owner.token, circleA, wonMatchId);
    await confirmMatch(p2.token, circleA, wonMatchId);
    await confirmMatch(p3.token, circleA, wonMatchId);
    await confirmMatch(p4.token, circleA, wonMatchId);

    // Partita lasciata pending (nessuna conferma extra) → azione urgente per owner
    await createMatch(
      owner.token, circleA,
      [owner.id, p3.id], [p2.id, p4.id],
      [{ team1: 6, team2: 4 }, { team1: 6, team2: 2 }],
    );

    // Login UI come owner
    await page.goto('/login');
    await page.fill('#email', owner.email);
    await page.fill('#password', 'testpass123');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/dashboard/);

    // AC1 — azioni urgenti visibili, con circolo di appartenenza
    await expect(page.locator('.attention')).toBeVisible();
    await expect(page.locator('.pending-card').first()).toContainText('DA CONFERMARE');
    await expect(page.locator('.pending-card .circle-tag').first()).toContainText('E2E_US063_A');

    // AC2 — card circolo attivo: rating, posizione, membri
    await expect(page.locator('.circle-picker')).toContainText('E2E_US063_A');
    await expect(page.locator('.rating-card')).toBeVisible();

    // AC3 — serie vittorie corrente (1 vittoria confirmed, 0 sconfitte)
    await expect(page.locator('.stats')).toContainText('1');

    // AC4 — ultime partite mostrate
    await expect(page.locator('.recent-list .recent').first()).toBeVisible();

    // AC5 — nessun CTA "+ Partita" duplicato fuori dalla bottom-nav
    await expect(page.locator('main.dashboard-content a:has-text("+ Partita")')).toHaveCount(0);

    // Nessun colore hard-coded rotto: bottom-nav visibile
    await expect(page.locator('.bottom-nav')).toBeVisible();
  });

  // AC6 originale (guardia sui 4 membri) è stato superato da una decisione successiva:
  // il "+" ora apre sempre Quick Match, che gestisce da solo scelta/creazione circolo
  // e giocatori ospiti, quindi nessun precheck sui membri è più necessario/corretto.
  test('CTA "+" apre sempre Quick Match, anche con circolo attivo sotto i 4 membri', async ({ page }) => {
    const owner = await registerUser(uniqueEmail('own2'), 'Owner2');
    const p2 = await registerUser(uniqueEmail('p2b'), 'Player2b');

    const circle = await createCircle(owner.token, `E2E_US063_LOW_${Date.now()}`);
    await joinCircle(p2.token, circle);

    await page.goto('/login');
    await page.fill('#email', owner.email);
    await page.fill('#password', 'testpass123');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/dashboard/);

    await expect(page.locator('.circle-picker')).toContainText(`E2E_US063_LOW`);

    await page.click('.bottom-nav .nav-action');
    await expect(page).toHaveURL(/\/match\/quick/);
  });
});
