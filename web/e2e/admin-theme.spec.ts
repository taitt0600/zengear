import { expect, test, type Page } from '@playwright/test'
import { accounts, PASSWORD } from './support/backend'
import { failOnCspViolations, signIn } from './support/pages'

// The theme of the admin (DEC-123, the Layout of SCR-ADM-01): "Giao diện" in the header, kept after a reload, set
// before the page paints by the inline script of next-themes, which runs only with the request's nonce (the CSP of
// /admin; failOnCspViolations), followed by the sign-in page. The browser's scheme is set, so that the result does not
// depend on the machine (DEC-123 (5)).

failOnCspViolations()
test.use({ colorScheme: 'light' })

const dark = /(^|\s)dark(\s|$)/

// The background of <body> against `color`, both drawn on a canvas and read back as sRGB bytes: the stylesheet writes
// the tokens in another notation (`lab(…)`) than the literal, so the serialized strings differ.
async function expectBackground(page: Page, color: string) {
  await expect
    .poll(() =>
      page.evaluate((expected) => {
        const context = document.createElement('canvas').getContext('2d')!
        const rgb = (value: string) => {
          context.clearRect(0, 0, 1, 1)
          context.fillStyle = value
          context.fillRect(0, 0, 1, 1)
          return [...context.getImageData(0, 0, 1, 1).data.slice(0, 3)]
        }
        const [actual, wanted] = [
          rgb(getComputedStyle(document.body).backgroundColor),
          rgb(expected),
        ]
        return Math.max(...actual.map((channel, i) => Math.abs(channel - wanted[i])))
      }, color),
    )
    .toBeLessThanOrEqual(1)
}

async function choose(page: Page, option: 'Sáng' | 'Tối' | 'Theo hệ thống') {
  await page.getByRole('button', { name: 'Giao diện' }).click()
  await page.getByRole('menuitemradio', { name: option }).click()
}

test('the Admin switches to the dark theme, which stays after a reload and on the sign-in page', async ({
  page,
}) => {
  const html = page.locator('html')
  await page.goto('/admin/sign-in')
  await signIn(page, accounts.admin, PASSWORD)
  await expect(page).toHaveURL('/admin/staff')
  await expect(html).not.toHaveClass(dark)
  await expectBackground(page, 'oklch(1 0 0)')

  await choose(page, 'Tối')
  await expect(html).toHaveClass(dark)
  // The softer background of DEC-124 (1).
  await expectBackground(page, 'oklch(0.235 0 0)')

  await page.reload()
  await expect(html).toHaveClass(dark)
  await expect(page.getByRole('navigation', { name: 'Menu' })).toBeVisible()
  // The scheme of the dark screenshot is set too (DEC-123 (5)); the choice "Tối" holds either way.
  await page.emulateMedia({ colorScheme: 'dark' })
  await page.screenshot({
    path: test.info().outputPath('admin-dark.png'),
    fullPage: true,
    animations: 'disabled',
  })
  await page.emulateMedia({ colorScheme: 'light' })

  // "Theo hệ thống" follows the browser's scheme, also when it changes.
  await choose(page, 'Theo hệ thống')
  await expect(html).not.toHaveClass(dark)
  await page.emulateMedia({ colorScheme: 'dark' })
  await expect(html).toHaveClass(dark)
  await page.emulateMedia({ colorScheme: 'light' })
  await expect(html).not.toHaveClass(dark)

  // SCR-AUT-01 has no switch of its own and follows the choice.
  await choose(page, 'Tối')
  await page.getByRole('button', { name: 'Chủ cửa hàng', exact: true }).click()
  await page.getByRole('menuitem', { name: 'Đăng xuất' }).click()
  await expect(page).toHaveURL(/\/admin\/sign-in/)
  await expect(page.getByRole('button', { name: 'Đăng nhập' })).toBeEnabled()
  await expect(html).toHaveClass(dark)
  await expect(page.getByRole('button', { name: 'Giao diện' })).toHaveCount(0)
  await page.emulateMedia({ colorScheme: 'dark' })
  await page.screenshot({
    path: test.info().outputPath('sign-in-dark.png'),
    fullPage: true,
    animations: 'disabled',
  })
})
