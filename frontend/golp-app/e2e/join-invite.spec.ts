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

async function createCircleAndGetInviteToken(ownerToken: string): Promise<{ circleId: string; inviteToken: string }> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${ownerToken}` } });
  const circleResp = await ctx.post(`${API}/circles`, { data: { name: `E2E_JOIN_${Date.now()}`, sport: 'padel' } });
  const circleId = (await circleResp.json()).id as string;

  const linkResp = await ctx.get(`${API}/circles/${circleId}/invite-link`);
  const inviteToken = (await linkResp.json()).inviteToken as string;
  await ctx.dispose();
  return { circleId, inviteToken };
}

test.describe('Join invite — US-015', () => {
  test('unauthenticated user opens /join?token=X — sees invite page with register/login CTAs', async ({ page }) => {
    const ownerToken = await registerUser(uniqueEmail('jo_owner'), 'Join Owner');
    const { inviteToken } = await createCircleAndGetInviteToken(ownerToken);

    await page.goto(`http://localhost:4200/join?token=${inviteToken}`);

    await expect(page.locator('h1')).toContainText('invitato', { timeout: 10000 });
    await expect(page.locator('a', { hasText: 'Registrati' })).toBeVisible();
    await expect(page.locator('a', { hasText: 'Accedi' })).toBeVisible();
  });

  test('invalid token — authenticated user sees error message', async ({ page }) => {
    const userEmail = uniqueEmail('jo_badtoken');
    await registerUser(userEmail, 'Bad Token User');

    await page.goto('http://localhost:4200/login');
    await page.fill('input[type="email"]', userEmail);
    await page.fill('input[type="password"]', 'testpass123');
    await page.click('button[type="submit"]');
    await page.waitForURL('**/dashboard');

    await page.goto('http://localhost:4200/join?token=invalidtoken123');

    await expect(page.locator('.form-error')).toContainText('non valido', { timeout: 10000 });
  });

  test('authenticated user opens /join?token=X — joins circle and redirects', async ({ page }) => {
    const ownerToken = await registerUser(uniqueEmail('jo_owner2'), 'Join Owner2');
    const { circleId, inviteToken } = await createCircleAndGetInviteToken(ownerToken);
    const memberEmail = uniqueEmail('jo_member');
    await registerUser(memberEmail, 'Join Member');

    // Login via browser
    await page.goto('http://localhost:4200/login');
    await page.fill('input[type="email"]', memberEmail);
    await page.fill('input[type="password"]', 'testpass123');
    await page.click('button[type="submit"]');
    await page.waitForURL('**/dashboard');

    // Navigate to join link
    await page.goto(`http://localhost:4200/join?token=${inviteToken}`);

    // Should redirect to My Circles page
    await page.waitForURL('**/circles', { timeout: 10000 });
    await expect(page.locator('h1')).toContainText('miei circoli', { timeout: 5000 });
  });
});
