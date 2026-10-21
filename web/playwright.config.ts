import { defineConfig, devices } from '@playwright/test'

const ci = !!process.env.CI

// Screen flows against the API, the Firebase Auth Emulator and the web (DEC-104 (7), (8)). The web runs on
// http://localhost:3000, the origin the API allows in Development (docs/API_CONVENTIONS.md, CORS). In CI the job
// acceptance builds the web first and these flows run against next start; locally against next dev.
export default defineConfig({
  testDir: 'e2e',
  // The flows share the Admin and the fixture accounts.
  workers: 1,
  retries: ci ? 1 : 0,
  forbidOnly: ci,
  reporter: ci ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: 'http://localhost:3000',
    locale: 'vi-VN',
    timezoneId: 'Asia/Ho_Chi_Minh',
    // Screenshots for the owner's test guide (DEC-100 (3)).
    screenshot: 'on',
    trace: 'on-first-retry',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: ci ? 'npm run start' : 'npm run dev',
    url: 'http://localhost:3000',
    reuseExistingServer: !ci,
    timeout: 120_000,
  },
})
