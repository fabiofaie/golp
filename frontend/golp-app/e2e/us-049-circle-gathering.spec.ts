import { test, expect, request as playwrightRequest } from '@playwright/test';

const API = 'http://localhost:5120';
const uniqueEmail = (prefix = 'u') => `${prefix}_${Date.now()}_${Math.random().toString(36).slice(2)}@e2e.test`;

async function registerUser(email: string, name: string): Promise<{ token: string; userId: string }> {
  const ctx = await playwrightRequest.newContext();
  const r = await ctx.post(`${API}/auth/register`, { data: { email, password: 'testpass123', name } });
  const body = await r.json();
  await ctx.dispose();
  return { token: body.token as string, userId: body.user?.id ?? body.userId };
}

async function createCircle(token: string): Promise<string> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${token}` } });
  const r = await ctx.post(`${API}/circles`, { data: { name: `E2E_GATHERING_${Date.now()}`, sport: 'padel' } });
  const body = await r.json();
  await ctx.dispose();
  return body.id as string;
}

async function joinCircle(token: string, circleId: string): Promise<void> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${token}` } });
  await ctx.post(`${API}/circles/${circleId}/join`);
  await ctx.dispose();
}

async function loginInBrowser(page: import('@playwright/test').Page, email: string): Promise<void> {
  await page.goto('http://localhost:4200/login');
  await page.fill('input[type="email"]', email);
  await page.fill('input[type="password"]', 'testpass123');
  await page.click('button[type="submit"]');
  await page.waitForURL('**/dashboard');
}

test.describe('Raduno al circolo — US-049', () => {
  test('check-in 4 presenti, genera piano, apre registrazione partita precompilata', async ({ page }) => {
    const ownerEmail = uniqueEmail('gath_owner');
    const owner = await registerUser(ownerEmail, 'Owner Raduno');
    const circleId = await createCircle(owner.token);

    for (const label of ['m1', 'm2', 'm3']) {
      const member = await registerUser(uniqueEmail(`gath_${label}`), `Membro ${label}`);
      await joinCircle(member.token, circleId);
    }

    await loginInBrowser(page, ownerEmail);
    await page.goto(`http://localhost:4200/circles/${circleId}/gathering`);

    await expect(page.locator('.member-chip')).toHaveCount(4, { timeout: 10000 });

    // check-in di tutti e 4 i presenti
    const chips = page.locator('.member-chip');
    const count = await chips.count();
    for (let i = 0; i < count; i++) {
      await chips.nth(i).click();
      await page.waitForTimeout(150); // attende la risposta dell'endpoint attendance
    }

    await expect(page.locator('.presence-counter strong')).toHaveText('4 presenti');

    // riduce l'obiettivo "partite totali" a 1 (default 4) cosi il piano genera un solo turno
    const targetValueStepper = page.locator('.stepper').nth(1);
    for (let i = 0; i < 3; i++) {
      await targetValueStepper.locator('button').first().click();
    }
    await expect(targetValueStepper.locator('.stepper-value')).toHaveText('1');

    const generateBtn = page.locator('button.generate-btn');
    await expect(generateBtn).toBeEnabled();
    await generateBtn.click();

    await expect(page.locator('.round-card')).toHaveCount(1, { timeout: 10000 });
    await expect(page.locator('.round-tab')).toHaveCount(1);

    await page.click('.confirm-round-btn');
    await page.waitForURL(/\/match\/new\?.*team1p1=/, { timeout: 10000 });

    // la registrazione partita segue il flusso esistente, invariato: submit richiede solo il risultato
    await expect(page.locator('h1, h2')).toContainText(/partita/i);
  });

  test('meno di 4 presenti — CTA disabilitata', async ({ page }) => {
    const ownerEmail = uniqueEmail('gath_owner_few');
    const owner = await registerUser(ownerEmail, 'Owner Poche Presenze');
    const circleId = await createCircle(owner.token);

    await loginInBrowser(page, ownerEmail);
    await page.goto(`http://localhost:4200/circles/${circleId}/gathering`);

    await expect(page.locator('.member-chip')).toHaveCount(1, { timeout: 10000 });
    await page.locator('.member-chip').first().click();

    const generateBtn = page.locator('button.generate-btn');
    await expect(generateBtn).toBeDisabled();
  });
});
