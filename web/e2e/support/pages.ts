import { expect, test, type Page } from '@playwright/test'

// Steps the screen flows share: the sign-in of SCR-AUT-01 and the CSP check of every flow.

export async function signIn(page: Page, email: string, password: string) {
  const button = page.getByRole('button', { name: 'Đăng nhập' })
  await expect(button).toBeEnabled()
  await page.getByLabel('Email công ty').fill(email)
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password)
  await button.click()
}

/**
 * Makes every flow of the file fail on a CSP violation: the CSP lets the pages reach the API and the emulator and run
 * the scripts of Next.js (DEC-104 (10), DEC-113 (6)).
 */
export function failOnCspViolations() {
  let blocked: string[] = []
  test.beforeEach(({ page }) => {
    blocked = []
    page.on('console', (message) => {
      if (message.type() === 'error' && /Content Security Policy/i.test(message.text())) {
        blocked.push(message.text())
      }
    })
  })
  test.afterEach(() => {
    expect(blocked).toEqual([])
  })
}
