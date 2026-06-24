import { defineConfig, devices } from '@playwright/test';

const CI = !!process.env['CI'];

export default defineConfig({
  testDir: './e2e',
  testMatch: ['auth.spec.ts', 'circle-leaderboard.spec.ts', 'circle-match-history.spec.ts', 'circle-awards.spec.ts', 'circle-stats.spec.ts', 'invite.spec.ts', 'join-invite.spec.ts', 'add-member.spec.ts', 'pwa-install.spec.ts', 'profile-theme.spec.ts', 'profile-push.spec.ts'],
  fullyParallel: false,
  forbidOnly: CI,
  retries: CI ? 1 : 0,
  workers: 1,
  reporter: CI ? 'list' : [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: 'http://localhost:4200',
    trace: 'on-first-retry',
    video: CI ? 'off' : 'retain-on-failure',
    headless: CI,
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } }
  ],
  webServer: {
    command: 'npx ng serve --port 4200 --proxy-config proxy.conf.js',
    url: 'http://localhost:4200',
    reuseExistingServer: true,
    timeout: 120000
  }
});
