import { expect, test, type Page } from '@playwright/test'

// Steps the screen flows share: the sign-in of SCR-AUT-01 and the CSP check of every flow.

/** Signs in with a username or an email, which the field "Tên đăng nhập hoặc email" takes alike (DEC-145 (4)). */
export async function signIn(page: Page, identifier: string, password: string) {
  const button = page.getByRole('button', { name: 'Đăng nhập' })
  await expect(button).toBeEnabled()
  // Anchored: the labels end with the ` *` of a required field.
  await page.getByLabel(/^Tên đăng nhập hoặc email/).fill(identifier)
  await page.getByLabel(/^Mật khẩu/).fill(password)
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

/**
 * Whether the document is taller than the window, so that it scrolls: a flow of the section navigation checks it first,
 * so that it tests the case it names (SCR-STF-03 and SCR-ACC-01, Section navigation).
 */
export const pageScrolls = (page: Page) =>
  page.evaluate(() => document.documentElement.scrollHeight > document.documentElement.clientHeight)
