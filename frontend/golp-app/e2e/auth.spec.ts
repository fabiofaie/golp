import { test, expect } from '@playwright/test';

const uniqueEmail = () => `e2e_${Date.now()}@test.com`;

test.describe('Auth smoke tests', () => {

  test('register → login → access protected route → logout', async ({ page }) => {
    const email = uniqueEmail();
    const password = 'testpassword1';

    // Register
    await page.goto('/register');
    await expect(page.locator('h1')).toContainText('Inizia a giocare');
    await page.fill('#name', 'E2E User');
    await page.fill('#email', email);
    await page.fill('#password', password);
    await page.click('button[type="submit"]');

    // After register → dashboard
    await expect(page).toHaveURL(/dashboard/);
    await expect(page.locator('h1')).toContainText('Ciao!');

    // Logout
    await page.click('button:has-text("Esci")');
    await expect(page).toHaveURL(/login/);

    // Login
    await page.fill('#email', email);
    await page.fill('#password', password);
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/dashboard/);
  });

  test('login with wrong password shows error', async ({ page }) => {
    await page.goto('/login');
    await page.fill('#email', 'nobody@test.com');
    await page.fill('#password', 'wrongpassword');
    await page.click('button[type="submit"]');

    await expect(page.locator('.form-error')).toBeVisible();
    await expect(page.locator('.form-error')).toContainText('Credenziali non valide');
  });

  test('duplicate email registration shows error', async ({ page }) => {
    const email = uniqueEmail();

    // First registration
    await page.goto('/register');
    await page.fill('#name', 'User1');
    await page.fill('#email', email);
    await page.fill('#password', 'password123');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/dashboard/);

    // Logout
    await page.click('button:has-text("Esci")');

    // Second registration with same email
    await page.goto('/register');
    await page.fill('#name', 'User2');
    await page.fill('#email', email);
    await page.fill('#password', 'password456');
    await page.click('button[type="submit"]');

    await expect(page.locator('.form-error')).toBeVisible();
  });

  test('forgot password page submits and shows success', async ({ page }) => {
    await page.goto('/forgot-password');
    await expect(page.locator('h1')).toContainText('Reset password');
    await page.fill('#email', 'test@example.com');
    await page.click('button[type="submit"]');

    await expect(page.locator('.form-success')).toBeVisible();
  });

  test('unauthenticated user redirected to login from dashboard', async ({ page }) => {
    // Clear storage
    await page.context().clearCookies();
    await page.goto('/dashboard');
    await expect(page).toHaveURL(/login/);
  });

  test('reset-password with invalid token shows error', async ({ page }) => {
    await page.goto('/reset-password?token=invalidtoken');
    await page.fill('#newPassword', 'newpassword1');
    await page.click('button[type="submit"]');

    await expect(page.locator('.form-error')).toBeVisible();
  });
});
