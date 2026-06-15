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

async function createCircle(token: string): Promise<string> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${token}` } });
  const r = await ctx.post(`${API}/circles`, { data: { name: `E2E_INV_${Date.now()}`, sport: 'padel' } });
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

test.describe('Invite link — US-014', () => {
  test('owner sees "Invita" button on their circle card', async ({ page }) => {
    const email = uniqueEmail('inv_owner');
    const token = await registerUser(email, 'Owner User');
    await createCircle(token);

    await loginInBrowser(page, email);
    await page.goto('http://localhost:4200/circles');

    await expect(page.locator('h1')).toContainText('miei circoli', { timeout: 10000 });
    await expect(page.locator('button', { hasText: 'Invita' })).toBeVisible({ timeout: 5000 });
  });

  test('non-owner member does not see "Invita" button', async ({ page }) => {
    const ownerEmail = uniqueEmail('inv_owner2');
    const memberEmail = uniqueEmail('inv_member');
    const ownerToken = await registerUser(ownerEmail, 'Owner2');
    const memberToken = await registerUser(memberEmail, 'Member');
    const circleId = await createCircle(ownerToken);
    await joinCircle(memberToken, circleId);

    await loginInBrowser(page, memberEmail);
    await page.goto('http://localhost:4200/circles');

    await expect(page.locator('h1')).toContainText('miei circoli', { timeout: 10000 });
    await expect(page.locator('button', { hasText: 'Invita' })).not.toBeVisible();
  });

  test('owner clicks "Invita" → dialog opens with visible URL', async ({ page }) => {
    const email = uniqueEmail('inv_dlg');
    const token = await registerUser(email, 'Dialog Owner');
    const circleId = await createCircle(token);

    await loginInBrowser(page, email);
    await page.goto('http://localhost:4200/circles');

    await expect(page.locator('button', { hasText: 'Invita' })).toBeVisible({ timeout: 10000 });
    await page.click('button:has-text("Invita")');

    await expect(page.locator('.invite-card')).toBeVisible({ timeout: 5000 });
    await expect(page.locator('.invite-url')).toContainText('/join?token=', { timeout: 10000 });
    await expect(page.locator('button', { hasText: 'Copia link' })).toBeVisible();
    await expect(page.locator('button', { hasText: 'Invia via email' })).toBeVisible();
  });
});
