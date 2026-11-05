import { expect, test, type Locator, type Page } from '@playwright/test'
import { accounts, meId, newSalesConsultant, PASSWORD } from './support/backend'
import { failOnCspViolations, signIn } from './support/pages'

// SCR-STF-03 Staff account (docs/SCREENS.md, STF-02) against the API, the Firebase Auth Emulator and the web: the
// Admin locks a new account with a last working day and restores it. Each flow makes its own account, so no reset.

failOnCspViolations()

async function signedInAdmin(page: Page) {
  await page.goto('/admin/sign-in')
  await signIn(page, accounts.admin, PASSWORD)
  await expect(page).toHaveURL('/admin/staff')
}

/** Today in Vietnam as the page shows it: dd/MM/yyyy. */
const today = () =>
  new Intl.DateTimeFormat('vi', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    timeZone: 'Asia/Ho_Chi_Minh',
  }).format(new Date())

/** Whether the page fits the window's width, with nothing to scroll sideways. */
const noSidewaysScroll = (page: Page) =>
  page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)

/** Whether an element's text stays within its own width: it wraps instead of spilling out. */
const textFits = (element: Locator) => element.evaluate((el) => el.scrollWidth <= el.clientWidth)

const card = (page: Page, title: string) =>
  page.locator('[data-slot=card]').filter({ has: page.getByRole('heading', { name: title }) })

test('the Admin locks an account with its last working day, then restores it', async ({ page }) => {
  const staff = await newSalesConsultant(`E2e-account-${Date.now()}`)
  await signedInAdmin(page)
  await page.goto(`/admin/staff/${staff.id}`)

  const status = card(page, 'Trạng thái tài khoản')
  // The value of "Ngày nghỉ việc" (the new account joins today, so its joined date shows today too).
  const leftDate = card(page, 'Thông tin nhân viên').locator('dt:text-is("Ngày nghỉ việc") + dd')
  await expect(page.getByRole('heading', { level: 1, name: /^E2E / })).toBeVisible()
  await expect(leftDate).toHaveText('—')
  await expect(page.getByRole('navigation', { name: 'Vị trí trang' })).toContainText(
    'Tài khoản nhân viên',
  )
  await expect(status.getByText('Đang hoạt động')).toBeVisible()
  await expect(status.getByText('Chưa đổi mật khẩu mặc định')).toBeVisible()
  await page.screenshot({ path: test.info().outputPath('staff-account.png'), fullPage: true })

  await status.getByRole('button', { name: 'Khóa tài khoản' }).click()
  const dialog = page.getByRole('dialog', { name: 'Khóa tài khoản' })
  await dialog.getByRole('checkbox', { name: 'Nhân viên nghỉ việc' }).click()
  await expect(dialog.getByRole('button', { name: /Ngày nghỉ việc/ })).toHaveText(today())
  await page.screenshot({ path: test.info().outputPath('staff-account-disable.png') })
  await dialog.getByRole('button', { name: 'Khóa', exact: true }).click()

  await expect(page.getByText('Đã khóa tài khoản.')).toBeVisible()
  await expect(dialog).toBeHidden()
  await expect(status.getByText('Đã khóa', { exact: true })).toBeVisible()
  await expect(leftDate).toHaveText(today())

  await status.getByRole('button', { name: 'Khôi phục tài khoản' }).click()
  await page
    .getByRole('alertdialog')
    .getByRole('button', { name: 'Khôi phục', exact: true })
    .click()
  await expect(page.getByText('Đã khôi phục tài khoản.')).toBeVisible()
  await expect(status.getByText('Đang hoạt động')).toBeVisible()
  await expect(leftDate).toHaveText('—')
})

test('on a narrow screen the status comes before the details, and a long email wraps', async ({
  page,
}) => {
  // About 60 characters before the `@` (at most 64, RFC 5321) and no hyphen, where a line could break anyway: wider
  // than a field of the details card, at 390 px and at `lg` alike.
  const staff = await newSalesConsultant(`E2e-narrow-${Date.now()}`, `e2e${'x'.repeat(40)}`)
  await page.setViewportSize({ width: 390, height: 844 })
  await signedInAdmin(page)
  await page.goto(`/admin/staff/${staff.id}`)

  const email = page.getByText(staff.email, { exact: true })
  await expect(email).toBeVisible()
  const status = await card(page, 'Trạng thái tài khoản').boundingBox()
  const details = await card(page, 'Thông tin nhân viên').boundingBox()
  expect(status!.y).toBeLessThan(details!.y)
  expect(Math.abs(status!.width - details!.width)).toBeLessThan(2)
  // The Layout: a long email wraps, never widens the card past the screen.
  expect(details!.x + details!.width).toBeLessThanOrEqual(390)
  expect(await noSidewaysScroll(page)).toBe(true)
  expect(await textFits(email)).toBe(true)
  await page.screenshot({
    path: test.info().outputPath('staff-account-narrow.png'),
    fullPage: true,
  })

  // From `lg` the details take two thirds and each field half of them: the text of the email, not only its box,
  // still fits its field.
  await page.setViewportSize({ width: 1280, height: 900 })
  expect(await textFits(email)).toBe(true)
  expect(await noSidewaysScroll(page)).toBe(true)
})

test('an unknown account leads back to the list, and the Admin cannot lock their own', async ({
  page,
}) => {
  await signedInAdmin(page)
  await page.goto('/admin/staff/0199a0f3-ffff-7fff-8fff-ffffffffffff')
  await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
  await page.getByRole('link', { name: 'Về danh sách nhân viên' }).click()
  await expect(page).toHaveURL('/admin/staff')

  await page.goto(`/admin/staff/${await meId(accounts.admin, PASSWORD)}`)
  await expect(page.getByText('Bạn không thể khóa tài khoản của chính mình.')).toBeVisible()
  await expect(page.getByRole('button', { name: 'Khóa tài khoản' })).toHaveCount(0)
})
