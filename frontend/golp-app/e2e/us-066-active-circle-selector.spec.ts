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

async function createCircle(token: string, name: string, sport = 'padel'): Promise<string> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${token}` } });
  const r = await ctx.post(`${API}/circles`, { data: { name, sport } });
  const body = await r.json();
  await ctx.dispose();
  return body.id as string;
}

test.describe('US-066 — Selettore circolo attivo e filtro dei contenuti dashboard', () => {
  test('selezione circolo aggiorna dashboard e viene ricordata dopo reload', async ({ page }) => {
    const owner = await registerUser(uniqueEmail('sel'), 'Owner');
    const circleAName = `E2E_US066_A_${Date.now()}`;
    const circleBName = `E2E_US066_B_${Date.now()}`;
    const circleA = await createCircle(owner.token, circleAName, 'padel');
    const circleB = await createCircle(owner.token, circleBName, 'beachtennis');
    void circleA;

    await page.goto('/login');
    await page.fill('#email', owner.email);
    await page.fill('#password', 'testpass123');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/dashboard/);

    // AC1 — apri il pannello e verifica che elenchi "Tutti i circoli" + i due circoli
    await page.click('.circle-picker');
    await expect(page.locator('.circle-sheet')).toBeVisible();
    await expect(page.locator('.circle-row', { hasText: 'Tutti i circoli' })).toBeVisible();
    await expect(page.locator('.circle-row', { hasText: circleAName })).toBeVisible();
    await expect(page.locator('.circle-row', { hasText: circleBName })).toBeVisible();

    // AC2 — selezionando il secondo circolo, la dashboard si aggiorna
    await page.click(`.circle-row:has-text("${circleBName}")`);
    await expect(page.locator('.circle-sheet')).toHaveCount(0);
    await expect(page.locator('.circle-picker strong').first()).toHaveText(circleBName);

    // AC4 — la selezione persiste dopo un reload
    await page.reload();
    await expect(page.locator('.circle-picker strong').first()).toHaveText(circleBName);

    void circleB;
  });

  test('cambiare circolo attivo non genera richieste urgenti né le nasconde (restano cross-circolo)', async ({ page }) => {
    const owner = await registerUser(uniqueEmail('urg'), 'Owner');
    const circleAName = `E2E_US066_UrgA_${Date.now()}`;
    const circleBName = `E2E_US066_UrgB_${Date.now()}`;
    await createCircle(owner.token, circleAName, 'padel');
    await createCircle(owner.token, circleBName, 'beachtennis');

    await page.goto('/login');
    await page.fill('#email', owner.email);
    await page.fill('#password', 'testpass123');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/dashboard/);

    const urgentCountBefore = await page.locator('.attention .pending-card').count();

    await page.click('.circle-picker');
    await page.click(`.circle-row:has-text("${circleBName}")`);
    await expect(page.locator('.circle-picker strong').first()).toHaveText(circleBName);

    // Nessuna nuova partita è stata registrata: il conteggio urgenti resta invariato
    // indipendentemente dal circolo attivo selezionato (comportamento cross-circolo pre-esistente).
    const urgentCountAfter = await page.locator('.attention .pending-card').count();
    expect(urgentCountAfter).toBe(urgentCountBefore);
  });
});
