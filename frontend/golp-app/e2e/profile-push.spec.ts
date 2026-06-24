import { test, expect } from '@playwright/test';

const uniqueEmail = (tag: string) => `e2e_push_${tag}_${Date.now()}@test.com`;

async function registerAndGoToProfile(page: import('@playwright/test').Page, name: string, email: string) {
  await page.goto('/register');
  await page.fill('#name', name);
  await page.fill('#email', email);
  await page.fill('#password', 'testpassword1');
  await page.click('button[type="submit"]');
  await expect(page).toHaveURL(/dashboard/);
  await page.click('a:has-text("Profilo")');
  await expect(page).toHaveURL(/profilo/);
}

test.describe('US-029 — toggle notifiche push dalla pagina Profilo', () => {

  test('app non installata → guida installazione invece del toggle', async ({ page }) => {
    const email = uniqueEmail('noinstall');
    await registerAndGoToProfile(page, 'Push User', email);

    // Browser headless di test non è standalone: deve apparire la guida, non il toggle
    await expect(page.locator('.push-toggle')).toHaveCount(0);
    const installBtn = page.locator('.btn-install');
    await expect(installBtn).toBeVisible();

    await installBtn.click();
    await expect(page.getByRole('dialog')).toBeVisible();
    await expect(page.locator('.install-guide__title')).toContainText('Installa Golp');
  });

  test('app installata + permesso concesso → toggle attivo, test-send disponibile', async ({ page }) => {
    const email = uniqueEmail('installed');

    // Simula PWA standalone + permesso già concesso + token già presente
    await page.addInitScript(() => {
      Object.defineProperty(window, 'matchMedia', {
        value: (query: string) => ({
          matches: query.includes('standalone'),
          media: query,
          addListener: () => {},
          removeListener: () => {},
          addEventListener: () => {},
          removeEventListener: () => {},
          dispatchEvent: () => false,
        }),
      });
      Object.defineProperty(Notification, 'permission', { value: 'granted', configurable: true });
      localStorage.setItem('golp_fcm_token', 'e2e-fake-token');
    });

    await registerAndGoToProfile(page, 'Push User 2', email);

    const toggle = page.locator('.push-toggle');
    await expect(toggle).toBeVisible();
    await expect(toggle).toHaveClass(/push-toggle--active/);
    await expect(toggle).toContainText('Attive');

    await expect(page.locator('.btn-test')).toBeVisible();
  });
});
