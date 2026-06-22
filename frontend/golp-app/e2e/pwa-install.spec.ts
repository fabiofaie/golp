import { test, expect } from '@playwright/test';

const ANDROID_CHROME_UA = 'Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36';
const uniqueEmail = () => `e2e_pwa_${Date.now()}@test.com`;

test.describe('PWA install banner (US-024)', () => {
  test.use({ userAgent: ANDROID_CHROME_UA, viewport: { width: 390, height: 844 } });

  test('primo accesso mobile → banner visibile → guida con step → dismiss → reload → banner non più visibile', async ({ page }) => {
    const email = uniqueEmail();
    const password = 'testpassword1';

    await page.goto('/register');
    await page.fill('#name', 'E2E PWA User');
    await page.fill('#email', email);
    await page.fill('#password', password);
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/dashboard/);

    await expect(page.locator('.install-banner')).toBeVisible();

    await page.click('.install-banner button:has-text("Scopri come")');
    await expect(page.locator('app-pwa-install-guide .step').first()).toBeVisible();

    await page.click('.install-guide__back');
    await expect(page.locator('app-pwa-install-guide')).toHaveCount(0);

    await page.click('.install-banner button:has-text("Non ora")');
    await expect(page.locator('.install-banner')).toHaveCount(0);

    await page.reload();
    await expect(page.locator('.install-banner')).toHaveCount(0);
  });
});
