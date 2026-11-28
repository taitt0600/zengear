import { randomInt } from 'node:crypto'
import { expect, test, type Page } from '@playwright/test'
import { changePassword, newSalesConsultant, renameMe, STAFF_NAME } from './support/backend'
import { failOnCspViolations, pageScrolls, signIn } from './support/pages'

// SCR-ACC-01 My account (docs/screens/SCR-ACC-01.md, STF-03) against the API, the Firebase Auth Emulator and the web.
// Each flow has a Sales Consultant of its own, whose password is already changed: the account page is their landing
// page.

failOnCspViolations()

async function signedInStaff(page: Page) {
  const stamp = `${Date.now()}${randomInt(1000)}`
  const initial = `E2e-initial-${stamp}`
  const password = `E2e-account-${stamp}`
  const staff = await newSalesConsultant(initial)
  await changePassword(staff.email, initial, password)
  await page.goto('/admin/sign-in')
  await signIn(page, staff.email, password)
  await expect(page).toHaveURL('/admin/account')
  return { ...staff, password }
}

const nameField = (page: Page) => page.getByLabel(/^Tên hiển thị/)
const save = (page: Page) => page.getByRole('button', { name: 'Lưu' })
const passwordCard = (page: Page) => page.getByRole('region', { name: 'Mật khẩu' })

/** Fills the card "Mật khẩu" and sends it. */
async function changeOnThePage(page: Page, current: string, next: string) {
  const card = passwordCard(page)
  await card.getByLabel(/^Mật khẩu hiện tại/).fill(current)
  await card.getByLabel(/^Mật khẩu mới/).fill(next)
  await card.getByLabel(/^Nhập lại mật khẩu mới/).fill(next)
  await card.getByRole('button', { name: 'Đổi mật khẩu' }).click()
}

/** Counts the requests of the password change that reach the API. */
function countChanges(page: Page) {
  const sent = { count: 0 }
  page.on('request', (request) => {
    if (request.method() === 'POST' && request.url().endsWith('/api/v1/admin/me/change-password'))
      sent.count += 1
  })
  return sent
}

test('a staff member renames their account and sees the name in the user menu', async ({
  page,
}) => {
  const staff = await signedInStaff(page)
  await expect(page.getByText(`${STAFF_NAME} · ${staff.email}`)).toBeVisible()
  await expect(nameField(page)).toHaveValue(STAFF_NAME)
  // The Email is the card's one row; a `definition` has no accessible name in ARIA 1.3, which Chromium follows.
  const profile = page.getByRole('region', { name: 'Thông tin tài khoản' })
  await expect(profile.getByRole('term')).toHaveText('Email')
  await expect(profile.getByRole('definition')).toHaveText(staff.email)

  await nameField(page).fill('   ')
  await save(page).click()
  await expect(page.getByText('Vui lòng nhập tên hiển thị.')).toBeVisible()

  await nameField(page).fill('  Nhân viên E2E  ')
  await save(page).click()
  await expect(page.getByText('Đã lưu.')).toBeVisible()
  await expect(nameField(page)).toHaveValue('Nhân viên E2E')
  // The user menu is named by the name and email it shows.
  await expect(page.getByRole('button', { name: `Nhân viên E2E ${staff.email}` })).toBeVisible()
  await page.screenshot({ path: test.info().outputPath('account.png'), fullPage: true })
})

test('a name changed elsewhere since the page loaded is not overwritten', async ({ page }) => {
  const staff = await signedInStaff(page)
  await expect(nameField(page)).toHaveValue(STAFF_NAME)

  await renameMe(staff.email, staff.password, 'Đổi ở thiết bị khác')
  await nameField(page).fill('Đổi trên màn hình này')
  await save(page).click()

  // The detail of the API's 412 (DEC-117 (4)), in a toast.
  await expect(page.getByText('Dữ liệu đã được người khác thay đổi. Đang tải lại.')).toBeVisible()
  await expect(nameField(page)).toHaveValue('Đổi ở thiết bị khác')
})

test('a staff member changes the password on the page with the current password', async ({
  page,
}) => {
  const staff = await signedInStaff(page)
  const changed = `E2e-changed-${Date.now()}`
  const newPassword = passwordCard(page).getByLabel(/^Mật khẩu mới/)
  await newPassword.fill(changed)
  await expect(newPassword).toHaveAccessibleDescription(/Từ 12 đến 128 ký tự \(đạt\)/)

  await changeOnThePage(page, staff.password, changed)

  await expect(page).toHaveURL('/admin/sign-in?reason=password-changed')
  await expect(
    page.getByText('Đã đổi mật khẩu. Vui lòng đăng nhập lại bằng mật khẩu mới.'),
  ).toBeVisible()
  await signIn(page, staff.email, changed)
  await expect(page).toHaveURL('/admin/account')
})

test('a wrong current password shows under its field and nothing is sent', async ({ page }) => {
  await signedInStaff(page)
  const sent = countChanges(page)

  await changeOnThePage(page, 'Sai-mat-khau-hien-tai', `E2e-changed-${Date.now()}`)

  const current = passwordCard(page).getByLabel(/^Mật khẩu hiện tại/)
  await expect(passwordCard(page).getByText('Mật khẩu hiện tại không đúng.')).toBeVisible()
  await expect(current).toBeFocused()
  await page.screenshot({ path: test.info().outputPath('account-password.png'), fullPage: true })
  expect(sent.count).toBe(0)
})

test('a 403 recent-login-required after the sign-in again shows its text, without retry', async ({
  page,
}) => {
  const staff = await signedInStaff(page)
  const detail =
    'Lần đăng nhập gần nhất đã quá 5 phút, nên cần xác nhận lại trước khi đổi mật khẩu.'
  // The API is another origin: with a route set, Playwright answers the CORS preflight and adds the CORS headers to a
  // fulfilled answer itself (Playwright issue #12929).
  let answered = 0
  await page.route('**/api/v1/admin/me/change-password', async (route) => {
    answered += 1
    await route.fulfill({
      status: 403,
      contentType: 'application/problem+json',
      body: JSON.stringify({
        type: '/problems/recent-login-required',
        title: 'Cần xác nhận lại mật khẩu',
        status: 403,
        detail,
        ruleId: 'R-USR-04',
      }),
    })
  })

  await changeOnThePage(page, staff.password, `E2e-changed-${Date.now()}`)

  await expect(page.getByText(detail)).toBeVisible()
  await expect(page.getByRole('dialog')).toHaveCount(0)
  await expect(page).toHaveURL('/admin/account')
  expect(answered).toBe(1)
})

test('the section navigation marks the card clicked, also where the page does not scroll', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 900 })
  await signedInStaff(page)
  const nav = page.getByRole('navigation', { name: 'Trên trang này' })
  const profileLink = nav.getByRole('link', { name: 'Thông tin tài khoản' })
  const passwordLink = nav.getByRole('link', { name: 'Mật khẩu' })
  await expect(nameField(page)).toHaveValue(STAFF_NAME)
  expect(await pageScrolls(page)).toBe(true)
  await expect(profileLink).toHaveAttribute('aria-current', 'location')

  await passwordLink.click()
  await expect(page.getByRole('heading', { level: 2, name: 'Mật khẩu' })).toBeFocused()
  await expect(page).toHaveURL(/\/admin\/account#password$/)
  await expect(passwordLink).toHaveAttribute('aria-current', 'location')
  await expect(profileLink).not.toHaveAttribute('aria-current')
  await page.screenshot({ path: test.info().outputPath('account-sections.png') })

  await page.setViewportSize({ width: 1440, height: 2000 })
  await page.goto('/admin/account')
  await expect(nameField(page)).toHaveValue(STAFF_NAME)
  expect(await pageScrolls(page)).toBe(false)
  await expect(profileLink).toHaveAttribute('aria-current', 'location')
  await passwordLink.click()
  await expect(page.getByRole('heading', { level: 2, name: 'Mật khẩu' })).toBeFocused()
  await expect(passwordLink).toHaveAttribute('aria-current', 'location')

  // Narrow: no navigation, the photo above the information, the button full width.
  await page.setViewportSize({ width: 390, height: 844 })
  await expect(nav).toBeHidden()
  await page.screenshot({ path: test.info().outputPath('account-narrow.png'), fullPage: true })
  await page.setViewportSize({ width: 2560, height: 1400 })
  await page.screenshot({ path: test.info().outputPath('account-wide.png'), fullPage: true })
})
