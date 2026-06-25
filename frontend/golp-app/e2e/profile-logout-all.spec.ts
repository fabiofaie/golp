import { test, expect, Browser } from '@playwright/test';

const uniqueEmail = () => `e2e_logoutall_${Date.now()}@test.com`;

async function registerAndGoToProfile(browser: Browser, name: string, email: string, password: string) {
  const context = await browser.newContext();
  const page = await context.newPage();
  await page.goto('/register');
  await page.fill('#name', name);
  await page.fill('#email', email);
  await page.fill('#password', password);
  await page.click('button[type="submit"]');
  await expect(page).toHaveURL(/dashboard/);
  return { context, page };
}

async function loginOnNewSession(browser: Browser, email: string, password: string) {
  const context = await browser.newContext();
  const page = await context.newPage();
  await page.goto('/login');
  await page.fill('#email', email);
  await page.fill('#password', password);
  await page.click('button[type="submit"]');
  await expect(page).toHaveURL(/dashboard/);
  return { context, page };
}

test.describe('US-031 — logout da tutti i device dal Profilo', () => {

  test('logout-all da una sessione invalida anche le altre sessioni attive', async ({ browser }) => {
    const email = uniqueEmail();
    const password = 'testpassword1';

    // Sessione A: registrazione
    const sessionA = await registerAndGoToProfile(browser, 'Logout All User', email, password);

    // Sessione B: login separato (stesso utente, secondo device)
    const sessionB = await loginOnNewSession(browser, email, password);

    // Sessione A: vai al profilo, conferma "Esci da tutti i device"
    await sessionA.page.click('a:has-text("Profilo")');
    await expect(sessionA.page).toHaveURL(/profilo/);
    await sessionA.page.click('button:has-text("Esci da tutti i device")');
    await expect(sessionA.page.locator('text=Confermi?')).toBeVisible();
    await sessionA.page.click('.logout-all-actions button:has-text("Conferma")');

    // Sessione A: reindirizzata al login (sessione corrente invalidata)
    await expect(sessionA.page).toHaveURL(/login/);

    // Sessione B: naviga verso una rotta autenticata che richiede una chiamata API
    // (il vecchio token viene rifiutato server-side, il refresh fallisce a sua volta, logout locale)
    await sessionB.page.goto('/circles');
    await sessionB.page.waitForTimeout(500);
    await sessionB.page.reload();
    await expect(sessionB.page).toHaveURL(/login/);

    await sessionA.context.close();
    await sessionB.context.close();
  });
});
