import { expect, test, type Page } from '@playwright/test'
import { changePassword, newSalesConsultant, renameMe } from './support/backend'
import { failOnCspViolations, signIn } from './support/pages'

// SCR-ACC-01 My account (docs/SCREENS.md, STF-03) against the API, the Firebase Auth Emulator and the web. Each flow
// has a Sales Consultant of its own, whose password is already changed: the account page is their landing page.

failOnCspViolations()

async function signedInStaff(page: Page) {
  const stamp = `${Date.now()}${Math.floor(Math.random() * 1000)}`
  const initial = `E2e-initial-${stamp}`
  const password = `E2e-account-${stamp}`
  const staff = await newSalesConsultant(initial)
  await changePassword(staff.email, initial, password)
  await page.goto('/admin/sign-in')
  await signIn(page, staff.email, password)
  await expect(page).toHaveURL('/admin/account')
  return { ...staff, password }
}

const nameField = (page: Page) => page.getByLabel('Tên hiển thị')
const save = (page: Page) => page.getByRole('button', { name: 'Lưu' })

test('a staff member renames their account and sees the name in the header', async ({ page }) => {
  const staff = await signedInStaff(page)
  await expect(nameField(page)).toHaveValue(/^E2E /)
  const email = page.getByLabel('Email', { exact: true })
  await expect(email).toHaveValue(staff.email)
  await expect(email).toHaveAttribute('readonly')
  await expect(
    page.getByText('Email là tên đăng nhập. Liên hệ quản trị viên khi cần đổi.'),
  ).toBeVisible()

  await nameField(page).fill('   ')
  await save(page).click()
  await expect(page.getByText('Vui lòng nhập tên hiển thị.')).toBeVisible()

  await nameField(page).fill('  Nhân viên E2E  ')
  await save(page).click()
  await expect(page.getByText('Đã lưu.')).toBeVisible()
  await expect(nameField(page)).toHaveValue('Nhân viên E2E')
  await expect(page.getByRole('button', { name: 'Nhân viên E2E' })).toBeVisible()
  await page.screenshot({ path: test.info().outputPath('account.png'), fullPage: true })
})

test('a name changed elsewhere since the page loaded is not overwritten', async ({ page }) => {
  const staff = await signedInStaff(page)
  await expect(nameField(page)).toHaveValue(/^E2E /)

  await renameMe(staff.email, staff.password, 'Đổi ở thiết bị khác')
  await nameField(page).fill('Đổi trên màn hình này')
  await save(page).click()

  await expect(page.getByText('Dữ liệu đã được người khác thay đổi. Đang tải lại.')).toBeVisible()
  await expect(nameField(page)).toHaveValue('Đổi ở thiết bị khác')
})

test('a staff member changes the password in the dialog, signing in again first', async ({
  page,
}) => {
  const staff = await signedInStaff(page)
  const changed = `E2e-changed-${Date.now()}`

  // A sign-in cannot age 5 minutes in a test: the first change is answered 403 recent-login-required (DEC-094). The
  // API is another origin: with a route set, Playwright answers the CORS preflight and adds the CORS headers to a
  // fulfilled answer itself (Playwright issue #12929).
  let answered = false
  await page.route('**/api/v1/admin/me/change-password', async (route) => {
    if (answered) return route.fallback()
    answered = true
    await route.fulfill({
      status: 403,
      contentType: 'application/problem+json',
      body: JSON.stringify({
        type: '/problems/recent-login-required',
        title: 'Recent sign-in required',
        status: 403,
        ruleId: 'R-USR-04',
      }),
    })
  })

  await page.getByRole('button', { name: 'Đổi mật khẩu' }).click()
  const dialog = page.getByRole('dialog', { name: 'Đổi mật khẩu' })
  await dialog.getByLabel('Mật khẩu mới', { exact: true }).fill(changed)
  await dialog.getByLabel('Nhập lại mật khẩu mới').fill(changed)
  await dialog.getByRole('button', { name: 'Đổi mật khẩu' }).click()

  const reauth = page.getByRole('dialog', {
    name: 'Vui lòng nhập lại mật khẩu hiện tại để tiếp tục',
  })
  await expect(reauth).toBeVisible()
  await page.screenshot({ path: test.info().outputPath('account-password.png'), fullPage: true })
  await reauth.getByLabel('Mật khẩu hiện tại').fill(staff.password)
  await reauth.getByRole('button', { name: 'Tiếp tục' }).click()

  await expect(page).toHaveURL('/admin/sign-in?reason=password-changed')
  await expect(
    page.getByText('Đã đổi mật khẩu. Vui lòng đăng nhập lại bằng mật khẩu mới.'),
  ).toBeVisible()
  expect(answered).toBe(true)

  await signIn(page, staff.email, changed)
  await expect(page).toHaveURL('/admin/account')
})
