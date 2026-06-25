import { test, expect } from '@playwright/test';

const uniqueEmail = () => `e2e_delete_${Date.now()}@test.com`;

test.describe('US-032 — eliminazione account dal Profilo', () => {

  test('flusso completo: password errata blocca, password corretta elimina e blocca il login', async ({ page }) => {
    const email = uniqueEmail();
    const password = 'testpassword1';

    await page.goto('/register');
    await page.fill('#name', 'Delete Account User');
    await page.fill('#email', email);
    await page.fill('#password', password);
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/dashboard/);

    await page.click('a:has-text("Profilo")');
    await expect(page).toHaveURL(/profilo/);

    await page.click('button:has-text("Elimina account")');
    await expect(page.locator('.delete-password-input')).toBeVisible();

    const confirmBtn = page.locator('.logout-all-actions button:has-text("Elimina definitivamente")');
    await expect(confirmBtn).toBeDisabled();

    // Password errata → messaggio esplicito, nessuna eliminazione
    await page.fill('.delete-password-input', 'wrongpassword');
    await expect(confirmBtn).toBeEnabled();
    await confirmBtn.click();
    await expect(page.locator('text=Password non valida')).toBeVisible();
    await expect(page).toHaveURL(/profilo/);

    // Password corretta → elimina e reindirizza al login
    await page.fill('.delete-password-input', password);
    await confirmBtn.click();
    await expect(page).toHaveURL(/login/);

    // Nuovo tentativo di login con le vecchie credenziali fallisce
    await page.fill('#email', email);
    await page.fill('#password', password);
    await page.click('button[type="submit"]');
    await expect(page.locator('.form-error')).toBeVisible();
  });
});
