import { defineConfig, devices } from '@playwright/test';
import { BASE_URL } from './support/env';

/**
 * E2E suite (testing plan §6.2). Runs against an already-running stack:
 *   E2E_BASE_URL  Angular UI  (default http://localhost:4200)
 *   E2E_API_URL   Claims API  (default http://localhost:5299) — used for setup/polling via the API.
 *
 *   npm run e2e         full suite
 *   npm run e2e:smoke   @smoke subset (E2E-01 happy path)
 */
export default defineConfig({
  testDir: './tests',
  outputDir: './test-results',
  // Tests create their own claims, so they're independent — but they share one API/DB, keep it modest.
  fullyParallel: true,
  workers: process.env['CI'] ? 2 : undefined,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 1 : 0,
  // GL posting is an async Hangfire job; the tests that wait for it poll for up to ~60s.
  timeout: 120_000,
  expect: { timeout: 10_000 },
  reporter: process.env['CI'] ? [['list'], ['html', { outputFolder: './playwright-report', open: 'never' }]] : 'list',
  use: {
    baseURL: BASE_URL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure'
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'], channel: process.env['E2E_CHROME_CHANNEL'] } }]
});
