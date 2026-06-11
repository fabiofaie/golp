import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir:       './e2e',
  testMatch:     ['verify-us-003.spec.ts'],
  fullyParallel: false,
  retries:       0,
  workers:       1,
  reporter:      [['list'], ['html', { open: 'never', outputFolder: 'playwright-report-us-003' }]],
  use: {
    baseURL: 'http://localhost:4200',
    trace:   'on-first-retry',
    video:   'on',
    headless: true,
    screenshot: 'on',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } }
  ],
  webServer: {
    command:            'npx ng serve --port 4200 --proxy-config proxy.conf.js',
    url:                'http://localhost:4200',
    reuseExistingServer: true,
    timeout:            120000,
  },
});
