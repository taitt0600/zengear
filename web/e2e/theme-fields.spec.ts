import { randomInt } from 'node:crypto'
import { expect, test, type Locator, type Page } from '@playwright/test'
import { changePassword, newSalesConsultant, STAFF_NAME } from './support/backend'
import { failOnCspViolations, signIn } from './support/pages'

// The fields of SCR-AUT-01 and SCR-ACC-01 in the states the theme tokens of :root draw: at rest (--input), focused
// (--ring) and in error (--destructive). src/app/globals.test.ts measures the contrast of --input and --ring; these
// flows check that the rendered borders are those tokens, and their screenshots show them in the test before merge.

failOnCspViolations()

// Transitions finish first, so that the halo of the focus and the colour of the button are drawn whole.
const shot = (page: Page, name: string) =>
  page.screenshot({
    path: test.info().outputPath(`${name}.png`),
    fullPage: true,
    animations: 'disabled',
  })

// The border a probe painted with the token gets, so that both colours are serialized the same way.
async function expectBorder(field: Locator, tokenName: string) {
  const value = await field.page().evaluate((name) => {
    const probe = document.createElement('div')
    probe.style.borderTop = `1px solid var(${name})`
    document.body.append(probe)
    const color = getComputedStyle(probe).borderTopColor
    probe.remove()
    return color
  }, `--${tokenName}`)
  await expect(field).toHaveCSS('border-top-color', value)
}

test('the sign-in fields, focused and in error', async ({ page }) => {
  await page.goto('/admin/sign-in')
  const button = page.getByRole('button', { name: 'Đăng nhập' })
  await expect(button).toBeEnabled()
  const identifier = page.getByLabel(/^Tên đăng nhập hoặc email/)
  await identifier.focus()
  await expect(identifier).toBeFocused()
  await expectBorder(identifier, 'ring')
  await expectBorder(page.getByLabel(/^Mật khẩu/), 'input')
  await shot(page, 'sign-in-field-focus')

  await button.click()
  await expect(identifier).toHaveAttribute('aria-invalid', 'true')
  await expect(page.getByText('Vui lòng nhập tên đăng nhập hoặc email.')).toBeVisible()
  await identifier.blur()
  await expectBorder(identifier, 'destructive')
  await shot(page, 'sign-in-field-error')
})

test('the account fields, focused and in error', async ({ page }) => {
  const stamp = `${Date.now()}${randomInt(1000)}`
  const initial = `E2e-initial-${stamp}`
  const password = `E2e-fields-${stamp}`
  const staff = await newSalesConsultant(initial)
  await changePassword(staff.email, initial, password)
  await page.goto('/admin/sign-in')
  await signIn(page, staff.email, password)
  await expect(page).toHaveURL('/admin/account')

  const name = page.getByLabel('Tên hiển thị')
  await expect(name).toHaveValue(STAFF_NAME)
  await name.focus()
  await expect(name).toBeFocused()
  await expectBorder(name, 'ring')
  // At rest: a field of the card "Mật khẩu" (the Email is a row now, no field).
  await expectBorder(page.getByLabel(/^Mật khẩu hiện tại/), 'input')
  await shot(page, 'account-field-focus')

  await name.fill('   ')
  await page.getByRole('button', { name: 'Lưu' }).click()
  await expect(name).toHaveAttribute('aria-invalid', 'true')
  await expect(page.getByText('Vui lòng nhập tên hiển thị.')).toBeVisible()
  await name.blur()
  await expectBorder(name, 'destructive')
  await shot(page, 'account-field-error')
})
