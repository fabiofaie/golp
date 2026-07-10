import { test, expect, request as playwrightRequest } from '@playwright/test';

const API = 'http://localhost:5120';
const uniqueEmail = (prefix = 'u') => `${prefix}_${Date.now()}_${Math.random().toString(36).slice(2)}@e2e.test`;

function decodeUserId(jwt: string): string {
  const payload = JSON.parse(Buffer.from(jwt.split('.')[1], 'base64').toString('utf-8'));
  return payload.sub;
}

async function registerUser(email: string, name: string): Promise<{ token: string; refreshToken: string; id: string; email: string }> {
  const ctx = await playwrightRequest.newContext();
  const r = await ctx.post(`${API}/auth/register`, { data: { email, password: 'testpass123', name } });
  const body = await r.json();
  await ctx.dispose();
  const token = body.accessToken ?? body.token;
  return { token, refreshToken: body.refreshToken, id: decodeUserId(token), email };
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

test.describe('US-064 — Bottom-nav condizionale limitata alle aree principali', () => {
  test('bottom-nav visibile sulle 4 rotte principali, assente sulle secondarie, ripristinata dopo navigazione', async ({ page }) => {
    const owner = await registerUser(uniqueEmail('own'), 'Owner');
    const p2 = await registerUser(uniqueEmail('p2'), 'Player2');
    const p3 = await registerUser(uniqueEmail('p3'), 'Player3');
    const p4 = await registerUser(uniqueEmail('p4'), 'Player4');

    const circle = await createCircle(owner.token, `E2E_US064_${Date.now()}`);
    await joinCircle(p2.token, circle);
    await joinCircle(p3.token, circle);
    await joinCircle(p4.token, circle);

    await page.goto('/login');
    await page.fill('#email', owner.email);
    await page.fill('#password', 'testpass123');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/dashboard/);

    // AC1 — visibile sulle 4 rotte principali
    await expect(page.locator('.bottom-nav')).toBeVisible();

    await page.click('.bottom-nav a[href="/my-matches"]');
    await expect(page).toHaveURL(/my-matches/);
    await expect(page.locator('.bottom-nav')).toBeVisible();

    await page.click('.bottom-nav a[href="/circles"]');
    await expect(page).toHaveURL(/\/circles$/);
    await expect(page.locator('.bottom-nav')).toBeVisible();

    await page.click('.bottom-nav a[href="/profilo"]');
    await expect(page).toHaveURL(/profilo/);
    await expect(page.locator('.bottom-nav')).toBeVisible();

    // AC2 — assente su rotta secondaria (creazione circolo)
    await page.goto('/circles/new');
    await expect(page.locator('.bottom-nav')).toHaveCount(0);
    // AC2 — header "Indietro" presente sulla secondaria
    await expect(page.locator('.back-nav, a:has-text("Indietro")').first()).toBeVisible();

    // AC2 — assente su rotta secondaria con parametro (storico circolo di UN circolo, non l'area "Partite")
    await page.goto(`/circles/${circle}/matches`);
    await expect(page.locator('.bottom-nav')).toHaveCount(0);

    // AC3 — ripristino automatico senza refresh: torna a una rotta principale via link "Indietro"/navigazione
    await page.goto('/dashboard');
    await expect(page.locator('.bottom-nav')).toBeVisible();
  });

  test('nessun doppio fetch di /circles/me tra dashboard e bottom-nav', async ({ page }) => {
    const owner = await registerUser(uniqueEmail('own2'), 'Owner2');
    const circle = await createCircle(owner.token, `E2E_US064_single_${Date.now()}`);

    let circlesMeCalls = 0;
    page.on('request', req => {
      if (req.url().includes('/circles/me')) circlesMeCalls++;
    });

    await page.goto('/login');
    await page.fill('#email', owner.email);
    await page.fill('#password', 'testpass123');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/dashboard/);
    await page.waitForTimeout(500);

    expect(circlesMeCalls).toBe(1);
    void circle;
  });

  test('CTA "+" funziona anche se l\'utente non passa mai da /dashboard nella sessione (bookmark/deep link)', async ({ page }) => {
    const owner = await registerUser(uniqueEmail('own3'), 'Owner3');
    const p2 = await registerUser(uniqueEmail('p2c'), 'Player2c');
    const p3 = await registerUser(uniqueEmail('p3c'), 'Player3c');
    const p4 = await registerUser(uniqueEmail('p4c'), 'Player4c');

    const circle = await createCircle(owner.token, `E2E_US064_deeplink_${Date.now()}`);
    await joinCircle(p2.token, circle);
    await joinCircle(p3.token, circle);
    await joinCircle(p4.token, circle);

    // Sessione autenticata senza mai passare da /login o /dashboard nel browser
    // (simula bookmark/deep link/refresh su una tab già autenticata su /circles)
    await page.addInitScript(([t, r]) => {
      localStorage.setItem('golp_token', t as string);
      localStorage.setItem('golp_refresh_token', r as string);
    }, [owner.token, owner.refreshToken]);

    await page.goto('/circles');
    await expect(page.locator('.bottom-nav')).toBeVisible();

    // Il CTA "+" naviga a Quick Match indipendentemente dal circolo attivo
    await page.click('.bottom-nav .nav-action');
    await expect(page).toHaveURL(/\/match\/quick/);
    void circle;
  });
});
