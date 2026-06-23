import { test, expect } from '@playwright/test';

const uniqueEmail = () => `e2e_theme_${Date.now()}@test.com`;

test.describe('US-028 — switch tema chiaro/scuro con persistenza', () => {

  test('default scuro → toggle chiaro → persiste dopo reload', async ({ page }) => {
    const email = uniqueEmail();
    const password = 'testpassword1';

    // Register → dashboard
    await page.goto('/register');
    await page.fill('#name', 'Theme User');
    await page.fill('#email', email);
    await page.fill('#password', password);
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/dashboard/);

    // Default scuro: nessuna classe theme-light sull'html
    const html = page.locator('html');
    await expect(html).not.toHaveClass(/theme-light/);

    // Vai a Profilo dal link in dashboard
    await page.click('a:has-text("Profilo")');
    await expect(page).toHaveURL(/profilo/);
    await expect(page.locator('h1')).toContainText('Profilo');

    // Stato iniziale: "Scuro" attivo
    await expect(page.locator('.theme-option--active')).toContainText('Scuro');

    // Sfondo scuro prima del cambio
    const bgDark = await page.evaluate(() =>
      getComputedStyle(document.documentElement).getPropertyValue('--color-bg').trim()
    );
    expect(bgDark).toBe('#0A0A0A');

    // Click su "Chiaro"
    await page.click('button:has-text("Chiaro")');

    // Classe applicata immediatamente
    await expect(html).toHaveClass(/theme-light/);
    const bgLight = await page.evaluate(() =>
      getComputedStyle(document.documentElement).getPropertyValue('--color-bg').trim()
    );
    expect(bgLight).toBe('#F7F5F2');

    // Reload → tema chiaro resta (persistenza localStorage)
    await page.reload();
    await expect(html).toHaveClass(/theme-light/);
    await expect(page.locator('.theme-option--active')).toContainText('Chiaro');
  });

  test('cambio tema applicato anche su altre pagine (dashboard)', async ({ page }) => {
    const email = uniqueEmail();
    const password = 'testpassword1';

    await page.goto('/register');
    await page.fill('#name', 'Theme User2');
    await page.fill('#email', email);
    await page.fill('#password', password);
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/dashboard/);

    // Imposta chiaro dal profilo
    await page.click('a:has-text("Profilo")');
    await page.click('button:has-text("Chiaro")');
    await expect(page.locator('html')).toHaveClass(/theme-light/);

    // Torna alla dashboard: tema chiaro persiste cross-pagina
    await page.click('a:has-text("Indietro")');
    await expect(page).toHaveURL(/dashboard/);
    await expect(page.locator('html')).toHaveClass(/theme-light/);
  });
});
