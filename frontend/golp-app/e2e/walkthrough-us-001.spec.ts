import { test, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const OUT_DIR = path.resolve(__dirname, '../../../docs/test-results/US-001');
const uniqueEmail = () => `walk_${Date.now()}@test.com`;

test('happy path — register → login → logout → reset password flow', async ({ page }) => {
  const email = uniqueEmail();
  const password = 'testpassword1';

  // 1. Register
  await page.goto('/register');
  await page.waitForLoadState('networkidle');
  await page.fill('#name', 'Marco Rossi');
  await page.fill('#email', email);
  await page.fill('#password', password);
  await page.click('button[type="submit"]');
  await expect(page).toHaveURL(/dashboard/);
  await page.waitForTimeout(500);

  // 2. Logout
  await page.click('button:has-text("Esci")');
  await expect(page).toHaveURL(/login/);
  await page.waitForTimeout(500);

  // 3. Login
  await page.fill('#email', email);
  await page.fill('#password', password);
  await page.click('button[type="submit"]');
  await expect(page).toHaveURL(/dashboard/);
  await page.waitForTimeout(500);

  // 4. Forgot password
  await page.goto('/forgot-password');
  await page.fill('#email', email);
  await page.click('button[type="submit"]');
  await expect(page.locator('.form-success')).toBeVisible();
  await page.waitForTimeout(800);

  // 5. Copy video to docs/test-results
  // Video is saved by Playwright to the test output dir automatically
});
