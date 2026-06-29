import { test, expect } from '@playwright/test';

const uniqueEmail = () => `e2e_circles_summary_${Date.now()}@test.com`;

test.describe('US-033 — Riepilogo circoli nel Profilo', () => {

  test('utente senza circoli vede messaggio "Non sei ancora membro"', async ({ page }) => {
    const email = uniqueEmail();
    const password = 'testpassword1';

    await page.goto('/register');
    await page.fill('#name', 'NoCircle User');
    await page.fill('#email', email);
    await page.fill('#password', password);
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/dashboard/);

    await page.click('a:has-text("Profilo")');
    await expect(page).toHaveURL(/profilo/);

    await expect(page.locator('.circles-hint')).toContainText('Non sei ancora membro');
    await expect(page.locator('.circle-row')).toHaveCount(0);
  });

  test('utente con circolo vede nome e rating, click naviga a storico partite', async ({ page }) => {
    const email = uniqueEmail();
    const password = 'testpassword1';

    await page.goto('/register');
    await page.fill('#name', 'Circle User');
    await page.fill('#email', email);
    await page.fill('#password', password);
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/dashboard/);

    // Crea un circolo
    await page.goto('/circles/new');
    await page.fill('#circle-name', 'Test Padel');
    await page.locator('select#circle-sport').selectOption('padel');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/circles/);

    // Vai al profilo
    await page.goto('/profilo');
    await expect(page.locator('.circle-row')).toHaveCount(1);
    await expect(page.locator('.circle-name').first()).toContainText('Test Padel');
    await expect(page.locator('.circle-rating').first()).toContainText('1000');

    // Click naviga a /circles/:id/matches
    await page.locator('.circle-row').first().click();
    await expect(page).toHaveURL(/circles\/.*\/matches/);
  });

});
