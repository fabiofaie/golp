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
  const r = await ctx.post(`${API}/circles`, { data: { name: `E2E_Guest_${Date.now()}`, sport: 'basket2v2' } });
  const body = await r.json();
  await ctx.dispose();
  return body.id as string;
}

async function joinCircle(token: string, circleId: string): Promise<void> {
  const ctx = await playwrightRequest.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${token}` } });
  await ctx.post(`${API}/circles/${circleId}/join`);
  await ctx.dispose();
}

test.describe('US-039 — record-match with guest player', () => {
  test('happy path: registra partita con ospite, badge (non registrato) appare in storico', async ({ page }) => {
    // Setup: 3 registered members + 1 guest slot filled via UI
    const ownerEmail = uniqueEmail('own');
    const p2Email    = uniqueEmail('p2');
    const p3Email    = uniqueEmail('p3');

    const ownerToken = await registerUser(ownerEmail, 'Owner');
    const p2Token    = await registerUser(p2Email, 'Player2');
    const p3Token    = await registerUser(p3Email, 'Player3');

    const circleId = await createCircle(ownerToken);
    await joinCircle(p2Token, circleId);
    await joinCircle(p3Token, circleId);

    // Login as owner
    await page.goto('/login');
    await page.fill('#email', ownerEmail);
    await page.fill('#password', 'testpass123');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/dashboard/);

    // Navigate to record-match page
    await page.goto(`/circles/${circleId}/record-match`);
    await expect(page.locator('.player-slot-card').first()).toBeVisible();

    // Slot 0: select owner (auto-selected in dropdown or select manually)
    // Slot 0 — leave as Membro, select owner
    await page.locator('.player-slot-card').nth(0).locator('select').selectOption({ label: 'Owner' });

    // Slot 1 — Membro: select Player2
    await page.locator('.player-slot-card').nth(1).locator('select').selectOption({ label: 'Player2' });

    // Slot 2 — Membro: select Player3
    await page.locator('.player-slot-card').nth(2).locator('select').selectOption({ label: 'Player3' });

    // Slot 3 — toggle to Ospite
    const slot3 = page.locator('.player-slot-card').nth(3);
    await slot3.locator('.slot-toggle-btn--guest').click();

    // Guest fields should appear
    await expect(slot3.locator('input[placeholder="Nome *"]')).toBeVisible();

    // Fill guest info
    const guestEmail = uniqueEmail('guest');
    await slot3.locator('input[placeholder="Nome *"]').fill('Ospite Test');
    await slot3.locator('input[placeholder="Email"]').fill(guestEmail);

    // Fill score (basket2v2: single score)
    await expect(page.locator('.score-single-input--t1')).toBeVisible();
    await page.locator('.score-single-input--t1').fill('21');
    await page.locator('.score-single-input--t2').fill('5');

    // Submit
    await page.click('button:has-text("Inserisci partita")');

    // Should redirect to /circles (my-circles page)
    await expect(page).toHaveURL(/\/circles/, { timeout: 8000 });

    // Navigate to match history for this circle
    await page.goto(`/circles/${circleId}/matches`);
    await expect(page.locator('.match-card').first()).toBeVisible();

    // Badge "(non registrato)" should appear for the guest
    await expect(page.locator('.unreg-badge').first()).toBeVisible();
    await expect(page.locator('.unreg-badge').first()).toContainText('non registrato');
  });

  test('slot membro/ospite toggle mostra e nasconde campi guest', async ({ page }) => {
    const ownerEmail = uniqueEmail('tog');
    const ownerToken = await registerUser(ownerEmail, 'Toggler');
    const circleId = await createCircle(ownerToken);

    await page.goto('/login');
    await page.fill('#email', ownerEmail);
    await page.fill('#password', 'testpass123');
    await page.click('button[type="submit"]');

    await page.goto(`/circles/${circleId}/record-match`);
    await expect(page.locator('.player-slot-card').first()).toBeVisible();

    const slot0 = page.locator('.player-slot-card').nth(0);

    // Default: membro → dropdown visible, guest fields hidden
    await expect(slot0.locator('select')).toBeVisible();
    await expect(slot0.locator('input[placeholder="Nome *"]')).not.toBeVisible();

    // Toggle to Ospite → guest fields visible, dropdown hidden
    await slot0.locator('.slot-toggle-btn--guest').click();
    await expect(slot0.locator('input[placeholder="Nome *"]')).toBeVisible();
    await expect(slot0.locator('select')).not.toBeVisible();

    // Toggle back to Membro → dropdown visible again
    await slot0.locator('.slot-toggle-btn').first().click();
    await expect(slot0.locator('select')).toBeVisible();
    await expect(slot0.locator('input[placeholder="Nome *"]')).not.toBeVisible();
  });
});
