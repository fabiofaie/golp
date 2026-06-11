import { test, expect, Page } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const OUT_DIR = path.resolve(__dirname, '../../../docs/test-results/US-001');
const SS_DIR = path.join(OUT_DIR, 'screenshots');

const consoleErrors: string[] = [];
const networkFailures: string[] = [];
const uniqueEmail = () => `verify_${Date.now()}@test.com`;

test.describe.configure({ mode: 'serial' });

let sharedEmail = '';
let sharedPassword = 'testpassword1';

test.beforeAll(() => {
  sharedEmail = uniqueEmail();
  fs.mkdirSync(SS_DIR, { recursive: true });
});

function attachListeners(page: Page) {
  page.on('console', msg => {
    if (msg.type() === 'error' || msg.type() === 'warning') {
      consoleErrors.push(`[${msg.type().toUpperCase()}] ${msg.text()}`);
    }
  });
  page.on('response', response => {
    if (response.status() >= 400) {
      networkFailures.push(`${response.request().method()} ${response.url()} → ${response.status()}`);
    }
  });
}

// AC1 — Registrazione crea account e autentica subito
test('AC1 — register flow', async ({ page }) => {
  attachListeners(page);
  await page.goto('/register');
  await expect(page.locator('h1')).toContainText('Inizia a giocare');
  await page.screenshot({ path: path.join(SS_DIR, 'AC1-register-page.png'), fullPage: true });

  await page.fill('#name', 'Marco Rossi');
  await page.fill('#email', sharedEmail);
  await page.fill('#password', sharedPassword);
  await page.screenshot({ path: path.join(SS_DIR, 'AC1-register-filled.png'), fullPage: true });

  await page.click('button[type="submit"]');
  await expect(page).toHaveURL(/dashboard/);
  await page.screenshot({ path: path.join(SS_DIR, 'AC1-dashboard-after-register.png'), fullPage: true });
});

// AC2 — Login valido → JWT; credenziali errate → errore senza rivelare campo
test('AC2 — login valid + invalid', async ({ page }) => {
  attachListeners(page);

  // Wrong credentials
  await page.goto('/login');
  await page.fill('#email', sharedEmail);
  await page.fill('#password', 'wrongpassword');
  await page.click('button[type="submit"]');
  await expect(page.locator('.form-error')).toBeVisible();
  await page.screenshot({ path: path.join(SS_DIR, 'AC2-login-error.png'), fullPage: true });

  // Valid credentials
  await page.fill('#password', sharedPassword);
  await page.click('button[type="submit"]');
  await expect(page).toHaveURL(/dashboard/);
  await page.screenshot({ path: path.join(SS_DIR, 'AC2-login-success-dashboard.png'), fullPage: true });
});

// AC3 — Email duplicata → 409 UI
test('AC3 — duplicate email shows error', async ({ page }) => {
  attachListeners(page);
  await page.goto('/register');
  await page.fill('#name', 'Altro Utente');
  await page.fill('#email', sharedEmail);
  await page.fill('#password', 'anotherpass1');
  await page.click('button[type="submit"]');
  await expect(page.locator('.form-error')).toBeVisible();
  await expect(page.locator('.form-error')).toContainText('già registrata');
  await page.screenshot({ path: path.join(SS_DIR, 'AC3-duplicate-email-error.png'), fullPage: true });
});

// AC4 — Password < 8 → form invalido (validazione client)
test('AC4 — short password validation', async ({ page }) => {
  attachListeners(page);
  await page.goto('/register');
  await page.fill('#name', 'Test');
  await page.fill('#email', 'shortpw@test.com');
  await page.fill('#password', '123');
  await page.click('button[type="submit"]');
  // Client-side validation: password field gets ng-invalid.ng-touched
  const input = page.locator('#password');
  await expect(input).toHaveClass(/ng-invalid/);
  await page.screenshot({ path: path.join(SS_DIR, 'AC4-short-password-validation.png'), fullPage: true });
});

// AC5 — Token assente blocca route protetta
test('AC5 — unauthenticated redirect to login', async ({ page }) => {
  attachListeners(page);
  await page.context().clearCookies();
  // Clear localStorage
  await page.goto('/');
  await page.evaluate(() => localStorage.clear());
  await page.goto('/dashboard');
  await expect(page).toHaveURL(/login/);
  await page.screenshot({ path: path.join(SS_DIR, 'AC5-protected-redirect.png'), fullPage: true });
});

// AC6 — Forgot password page + success message (no-enumeration)
test('AC6 — forgot password flow UI', async ({ page }) => {
  attachListeners(page);
  await page.goto('/forgot-password');
  await expect(page.locator('h1')).toContainText('Reset password');
  await page.screenshot({ path: path.join(SS_DIR, 'AC6-forgot-password-page.png'), fullPage: true });

  await page.fill('#email', sharedEmail);
  await page.click('button[type="submit"]');
  // Success message should appear (no enumeration)
  await expect(page.locator('.form-success')).toBeVisible();
  await page.screenshot({ path: path.join(SS_DIR, 'AC6-forgot-password-success.png'), fullPage: true });
});

// AC7 — Reset password page reads token from URL
test('AC7 — reset password invalid token error', async ({ page }) => {
  attachListeners(page);
  await page.goto('/reset-password?token=fakeinvalidtoken');
  await expect(page.locator('h1')).toContainText('Nuova password');
  await page.fill('#newPassword', 'newpassword1');
  await page.click('button[type="submit"]');
  await expect(page.locator('.form-error')).toBeVisible();
  await page.screenshot({ path: path.join(SS_DIR, 'AC7-reset-invalid-token.png'), fullPage: true });
});

test.afterAll(() => {
  fs.writeFileSync(
    path.join(OUT_DIR, 'console.log'),
    consoleErrors.length > 0 ? consoleErrors.join('\n') : '(nessun errore/warning console)\n'
  );
  fs.writeFileSync(
    path.join(OUT_DIR, 'network.log'),
    networkFailures.length > 0 ? networkFailures.join('\n') : '(nessuna richiesta fallita)\n'
  );
});
