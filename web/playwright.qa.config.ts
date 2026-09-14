import { defineConfig, devices } from '@playwright/test'

export default defineConfig({
  testDir: './tests/qa',
  testMatch: /(?:keyboard|puzzle-feedback|responsive|overlays)\.qa\.ts/,
  fullyParallel: false,
  retries: 0,
  reporter: [['list']],
  use: {
    baseURL: 'http://localhost:5174',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  webServer: {
    command: 'VITE_ANL_TEST_TOOLS=1 npm run dev -- --port 5174 --strictPort',
    url: 'http://localhost:5174',
    reuseExistingServer: false,
    timeout: 60_000,
  },
  projects: [{ name: 'chromium-qa', use: { ...devices['Desktop Chrome'] } }],
})
