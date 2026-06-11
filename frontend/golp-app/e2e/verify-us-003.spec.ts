import { test, expect, Page, APIRequestContext } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const OUT_DIR = path.resolve(__dirname, '../../../docs/test-results/US-003');
const SS_DIR  = path.join(OUT_DIR, 'screenshots');
const API     = 'http://localhost:5120';

const consoleErrors:   string[] = [];
const networkFailures: string[] = [];

const uid = (p = 'u') => `${p}_${Date.now()}_${Math.random().toString(36).slice(2, 6)}@test.com`;

test.describe.configure({ mode: 'serial' });

let ownerToken  = '';
let joinerEmail = '';
const PWD = 'password12';

// ─── API helpers ─────────────────────────────────────────────────────────────

async function apiRegister(request: APIRequestContext, name: string, email: string) {
  const r = await request.post(`${API}/auth/register`, { data: { name, email, password: PWD } });
  const body = await r.json();
  return body.token as string;
}

async function apiCreateCircle(request: APIRequestContext, token: string, name: string, sport: string) {
  const r = await request.post(`${API}/circles`, {
    data: { name, sport },
    headers: { Authorization: `Bearer ${token}` },
  });
  const body = await r.json();
  return body.id as string;
}

// ─── SPA navigation helpers ───────────────────────────────────────────────────
// The proxy.conf.json routes ALL /circles/* to the backend.
// page.goto('/circles/browse') would hit the backend → 404 → blank page.
// Use SPA navigation (click Angular routerLinks) instead.

async function goToMyCircles(page: Page) {
  // Must be called when page is on /dashboard
  const link = page.locator('a[href="/circles"]').first();
  await expect(link).toBeVisible({ timeout: 10000 });
  await link.click();
  await expect(page.locator('h1')).toContainText('I miei circoli', { timeout: 15000 });
}

async function goToBrowse(page: Page) {
  // Must be called when page is on /dashboard
  await goToMyCircles(page);
  const browseLink = page.locator('a[href="/circles/browse"]').first();
  await expect(browseLink).toBeVisible({ timeout: 10000 });
  await browseLink.click();
  await expect(page.locator('h1')).toContainText('Scopri', { timeout: 15000 });
}

// ─── Auth helpers ─────────────────────────────────────────────────────────────

async function registerUI(page: Page, name: string, email: string) {
  await page.goto('/register');
  await expect(page.locator('h1')).toBeVisible({ timeout: 15000 });
  await page.fill('#name', name);
  await page.fill('#email', email);
  await page.fill('#password', PWD);
  await page.click('button[type="submit"]');
  await expect(page).toHaveURL(/dashboard/, { timeout: 15000 });
}

async function loginUI(page: Page, email: string) {
  await page.goto('/login');
  await expect(page.locator('h1')).toBeVisible({ timeout: 15000 });
  await page.fill('#email', email);
  await page.fill('#password', PWD);
  await page.click('button[type="submit"]');
  await expect(page).toHaveURL(/dashboard/, { timeout: 15000 });
}

// ─── Setup ───────────────────────────────────────────────────────────────────

test.beforeAll(async ({ request }) => {
  fs.mkdirSync(SS_DIR, { recursive: true });

  // Owner e circolo creati via API — setup deterministico e veloce
  ownerToken = await apiRegister(request, 'Circle Owner', uid('owner'));
  await apiCreateCircle(request, ownerToken, 'Padel Roma Verify', 'padel');
  joinerEmail = uid('joiner');
});

function listenPage(page: Page) {
  page.on('console', msg => {
    if (msg.type() === 'error' || msg.type() === 'warning')
      consoleErrors.push(`[${msg.type().toUpperCase()}] ${msg.text()}`);
  });
  page.on('response', res => {
    if (res.status() >= 400) {
      const url = res.url();
      if (!url.includes('favicon') && !url.includes('hot-update') && !url.includes('.js') && !url.includes('.css'))
        networkFailures.push(`${res.request().method()} ${url} → ${res.status()}`);
    }
  });
}

// ─── AC1: browse page carica ──────────────────────────────────────────────────

test('AC1 — Pagina Browse Circles carica e mostra i circoli', async ({ page }) => {
  listenPage(page);

  await registerUI(page, 'Joiner User', joinerEmail);
  await page.screenshot({ path: path.join(SS_DIR, 'AC1-dashboard.png'), fullPage: true });

  await goToBrowse(page);
  await page.screenshot({ path: path.join(SS_DIR, 'AC1-browse-page.png'), fullPage: true });

  const card = page.locator('.circle-card', { hasText: 'Padel Roma Verify' }).first();
  await expect(card).toBeVisible({ timeout: 10000 });
  await page.screenshot({ path: path.join(SS_DIR, 'AC1-circle-card-visible.png'), fullPage: true });
});

// ─── AC1: click join → badge Membro ──────────────────────────────────────────

test('AC1 — Click Unisciti → badge Membro', async ({ page }) => {
  listenPage(page);

  await loginUI(page, joinerEmail);
  await goToBrowse(page);

  const card = page.locator('.circle-card', { hasText: 'Padel Roma Verify' }).first();
  await expect(card).toBeVisible({ timeout: 10000 });

  const joinBtn = card.locator('.btn-join');
  await expect(joinBtn).toBeVisible({ timeout: 5000 });
  await page.screenshot({ path: path.join(SS_DIR, 'AC1-before-join.png'), fullPage: true });

  await joinBtn.click();

  await expect(card.locator('.member-badge')).toBeVisible({ timeout: 10000 });
  await expect(card.locator('.btn-join')).not.toBeVisible();
  await page.screenshot({ path: path.join(SS_DIR, 'AC1-after-join-member-badge.png'), fullPage: true });
});

// ─── AC3: My Circles con rating 1000 ──────────────────────────────────────────

test('AC3 — Circolo in My Circles con rating 1000', async ({ page }) => {
  listenPage(page);

  await loginUI(page, joinerEmail);
  await goToMyCircles(page);

  const circleCard = page.locator('.circle-card', { hasText: 'Padel Roma Verify' }).first();
  await expect(circleCard).toBeVisible({ timeout: 10000 });
  await expect(circleCard.locator('.rating-value')).toContainText('1000', { timeout: 5000 });
  await page.screenshot({ path: path.join(SS_DIR, 'AC3-my-circles-rating-1000.png'), fullPage: true });
});

// ─── AC4: doppio join → badge già visibile ────────────────────────────────────

test('AC4 — Doppia iscrizione: badge Membro già visibile', async ({ page }) => {
  listenPage(page);

  await loginUI(page, joinerEmail);
  await goToBrowse(page);

  const card = page.locator('.circle-card', { hasText: 'Padel Roma Verify' }).first();
  await expect(card).toBeVisible({ timeout: 10000 });

  // isAlreadyMember=true dal server → nessun pulsante join
  await expect(card.locator('.member-badge')).toBeVisible({ timeout: 5000 });
  await expect(card.locator('.btn-join')).not.toBeVisible();
  await page.screenshot({ path: path.join(SS_DIR, 'AC4-already-member-badge.png'), fullPage: true });
});

// ─── AC2: multi-circolo ───────────────────────────────────────────────────────

test('AC2 — Multi-circolo: joiner appartiene a 2 circoli', async ({ page, request }) => {
  listenPage(page);

  await loginUI(page, joinerEmail);

  // Estrai token joiner da localStorage e crea secondo circolo via API
  const joinerToken = await page.evaluate(() => localStorage.getItem('golp_token'));
  expect(joinerToken).toBeTruthy();
  await apiCreateCircle(request, joinerToken!, 'Beach Rimini Verify', 'beachtennis');

  // My Circles deve mostrare 2 circoli (SPA nav)
  await goToMyCircles(page);
  await expect(page.locator('.circle-card')).toHaveCount(2, { timeout: 10000 });
  await page.screenshot({ path: path.join(SS_DIR, 'AC2-two-circles.png'), fullPage: true });
});

// ─── AC5: isolamento ──────────────────────────────────────────────────────────

test('AC5 — Isolamento: My Circles mostra solo i circoli del joiner', async ({ page }) => {
  listenPage(page);

  await loginUI(page, joinerEmail);
  await goToMyCircles(page);

  await expect(page.locator('.circle-card')).toHaveCount(2, { timeout: 10000 });
  await expect(page.locator('.circle-card', { hasText: 'Padel Roma Verify' })).toBeVisible();
  await expect(page.locator('.circle-card', { hasText: 'Beach Rimini Verify' })).toBeVisible();
  await page.screenshot({ path: path.join(SS_DIR, 'AC5-isolated-circles.png'), fullPage: true });
});

// ─── UI: Empty state con link Scopri circoli ─────────────────────────────────

test('UI — Empty state mostra link Scopri circoli', async ({ page }) => {
  listenPage(page);

  const freshEmail = uid('fresh');
  await registerUI(page, 'Fresh User', freshEmail);
  await goToMyCircles(page);

  await expect(page.locator('.empty-state')).toBeVisible({ timeout: 10000 });
  await expect(page.locator('a[href*="browse"]')).toBeVisible({ timeout: 5000 });
  await page.screenshot({ path: path.join(SS_DIR, 'UI-empty-state-link.png'), fullPage: true });
});

// ─── Teardown ─────────────────────────────────────────────────────────────────

test.afterAll(() => {
  fs.writeFileSync(
    path.join(OUT_DIR, 'console.log'),
    consoleErrors.length > 0 ? consoleErrors.join('\n') : '(nessun errore/warning console)\n'
  );
  fs.writeFileSync(
    path.join(OUT_DIR, 'network.log'),
    networkFailures.length > 0 ? networkFailures.join('\n') : '(nessuna richiesta fallita)\n'
  );
});
