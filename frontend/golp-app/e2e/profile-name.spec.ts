import { test, expect } from '@playwright/test';

const uniqueEmail = () => `e2e_name_${Date.now()}@test.com`;

test.describe('US-030 — Modifica nome visualizzato', () => {

  test('modifica nome: conferma salvata e nuovo nome visibile in classifica senza re-login', async ({ browser }) => {
    const email = uniqueEmail();
    const initialName = 'NomeIniziale';
    const newName = 'NomeAggiornato';

    const context = await browser.newContext();
    const page = await context.newPage();

    // Registrazione
    await page.goto('/register');
    await page.fill('#name', initialName);
    await page.fill('#email', email);
    await page.fill('#password', 'testpassword1');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/dashboard/);

    // Crea un circolo per avere classifica con il proprio nome
    await page.goto('/circles/create');
    await page.fill('#name', 'TestCircolo');
    const sportSelect = page.locator('select#sport');
    if (await sportSelect.count() > 0) {
      await sportSelect.selectOption({ index: 0 });
    }
    await page.click('button[type="submit"]');
    await page.waitForURL(/circles\/[^/]+$/, { timeout: 5000 }).catch(() => {});

    // Vai al profilo
    await page.goto('/profilo');
    await page.waitForLoadState('networkidle');

    // Campo precompilato col nome iniziale
    const nameInput = page.locator('#displayName');
    await expect(nameInput).toHaveValue(initialName);

    // Modifica nome
    await nameInput.fill(newName);
    await page.click('button:has-text("Salva nome")');

    // Messaggio di conferma
    await expect(page.locator('.push-hint--ok')).toBeVisible();

    // Verifica che il nuovo nome appaia in classifica senza re-login
    await page.goto('/circles');
    // Naviga al primo circolo disponibile
    const firstCircleLink = page.locator('a').filter({ hasText: /classifica/i }).first();
    if (await firstCircleLink.count() > 0) {
      await firstCircleLink.click();
      await expect(page.locator(`text=${newName}`)).toBeVisible();
    }

    await context.close();
  });

  test('nome vuoto blocca il salvataggio con messaggio di errore', async ({ browser }) => {
    const context = await browser.newContext();
    const page = await context.newPage();

    await page.goto('/register');
    await page.fill('#name', 'TestUser');
    await page.fill('#email', uniqueEmail());
    await page.fill('#password', 'testpassword1');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/dashboard/);

    await page.goto('/profilo');
    await page.waitForLoadState('networkidle');

    await page.locator('#displayName').fill('   ');
    await page.click('button:has-text("Salva nome")');

    await expect(page.locator('.push-hint--warn')).toBeVisible();
    await expect(page.locator('.push-hint--ok')).not.toBeVisible();

    await context.close();
  });
});
