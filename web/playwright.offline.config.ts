import { defineConfig, devices } from '@playwright/test'

export default defineConfig({
  testDir: './tests/qa',
  testMatch: 'offline.qa.ts',
  fullyParallel: false,
  retries: 0,
  reporter: [['list']],
  use: {
    baseURL: 'http://localhost:4173',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  webServer: {
    command: 'npm run preview -- --port 4173 --strictPort',
    url: 'http://localhost:4173',
    reuseExistingServer: false,
    timeout: 60_000,
  },
  projects: [{ name: 'chromium-offline', use: { ...devices['Desktop Chrome'] } }],
})
