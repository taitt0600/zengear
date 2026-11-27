import { expect, test, type Locator, type Page } from '@playwright/test'
import {
  accounts,
  meId,
  newPendingAccount,
  newSalesConsultant,
  PASSWORD,
  STAFF_NAME,
} from './support/backend'
import { failOnCspViolations, signIn } from './support/pages'

// SCR-STF-03 Staff account (docs/screens/SCR-STF-03.md, STF-02) against the API, the Firebase Auth Emulator and the
// web: the Admin locks a new account with a last working day and restores it. Each flow makes its own account, so no
// reset.

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
  page
    .locator('[data-slot=card]')
    .filter({ has: page.getByRole('heading', { name: title, exact: true }) })

test('the Admin locks an account with its last working day, then restores it', async ({ page }) => {
  const staff = await newSalesConsultant(`E2e-account-${Date.now()}`)
  await signedInAdmin(page)
  await page.goto(`/admin/staff/${staff.id}`)

  const status = card(page, 'Tài khoản')
  // The value of "Ngày nghỉ việc" (the new account joins today, so its joined date shows today too).
  const leftDate = card(page, 'Hồ sơ nhân viên').locator('dt:text-is("Ngày nghỉ việc") + dd')
  await expect(page.getByRole('heading', { level: 1, name: STAFF_NAME })).toBeVisible()
  await expect(leftDate).toHaveText('—')
  // The frame of SCR-ADM-01: "Nhân viên › Danh sách nhân viên › Tài khoản nhân viên", the entry a link.
  const breadcrumb = page.getByRole('navigation', { name: 'Vị trí trang' })
  await expect(breadcrumb.getByRole('link', { name: 'Danh sách nhân viên' })).toHaveAttribute(
    'href',
    '/admin/staff',
  )
  await expect(breadcrumb).toContainText('Tài khoản nhân viên')
  await expect(status.getByText('Đang hoạt động')).toBeVisible()
  await expect(status.locator('dt:text-is("Mật khẩu mặc định") + dd')).toHaveText('Chưa đổi')
  await page.screenshot({ path: test.info().outputPath('staff-account.png'), fullPage: true })

  await status.getByRole('button', { name: 'Khóa tài khoản' }).click()
  const dialog = page.getByRole('dialog', { name: 'Khóa tài khoản' })
  await expect(dialog.getByText(/Trường có dấu/)).toHaveCount(0)
  await dialog.getByRole('checkbox', { name: 'Nhân viên nghỉ việc' }).click()
  await expect(dialog.getByRole('button', { name: /Ngày nghỉ việc/ })).toHaveText(today())
  await expect(dialog.getByText(/Trường có dấu/)).toBeVisible()
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

test('on a narrow screen the cards keep their order without the navigation, and a long email wraps', async ({
  page,
}) => {
  // A given name of 50 letters gives 59 characters before the `@` (at most 64, RFC 5321, R-USR-24) and no hyphen, where
  // a line could break anyway: wider than the value of a row of the card "Tài khoản", at 390 px and at `xl` alike.
  const staff = await newSalesConsultant(`E2e-narrow-${Date.now()}`, `Nguyễn ${'X'.repeat(50)}`)
  await page.setViewportSize({ width: 390, height: 844 })
  await signedInAdmin(page)
  await page.goto(`/admin/staff/${staff.id}`)

  const email = card(page, 'Tài khoản').getByText(staff.email, { exact: true })
  await expect(email).toBeVisible()
  await expect(page.getByRole('navigation', { name: 'Trên trang này' })).toBeHidden()
  const details = await card(page, 'Hồ sơ nhân viên').boundingBox()
  const account = await card(page, 'Tài khoản').boundingBox()
  expect(details!.y).toBeLessThan(account!.y)
  expect(Math.abs(account!.width - details!.width)).toBeLessThan(2)
  // The Layout: a long email wraps, never widens the card past the screen.
  expect(account!.x + account!.width).toBeLessThanOrEqual(390)
  expect(await noSidewaysScroll(page)).toBe(true)
  expect(await textFits(email)).toBe(true)
  await page.screenshot({
    path: test.info().outputPath('staff-account-narrow.png'),
    fullPage: true,
  })

  // At `xl` with the menu expanded the cards are at their narrowest beside the navigation: the text of the email, not
  // only its box, still fits its row.
  await page.setViewportSize({ width: 1280, height: 900 })
  await expect(page.getByRole('navigation', { name: 'Trên trang này' })).toBeVisible()
  expect(await textFits(email)).toBe(true)
  expect(await noSidewaysScroll(page)).toBe(true)
})

test('the section navigation leads to a card, and the page keeps a column of 1024 px', async ({
  page,
}) => {
  const staff = await newSalesConsultant(`E2e-sections-${Date.now()}`)
  // A window low enough for the page to scroll.
  await page.setViewportSize({ width: 1440, height: 600 })
  await signedInAdmin(page)
  await page.goto(`/admin/staff/${staff.id}`)

  const nav = page.getByRole('navigation', { name: 'Trên trang này' })
  const profileLink = nav.getByRole('link', { name: 'Hồ sơ nhân viên' })
  const accountLink = nav.getByRole('link', { name: 'Tài khoản' })
  await expect(profileLink).toHaveAttribute('aria-current', 'location')
  await accountLink.click()
  await expect(page.getByRole('heading', { level: 2, name: 'Tài khoản' })).toBeFocused()
  await expect(page).toHaveURL(new RegExp(`/admin/staff/${staff.id}#account$`))
  // The end of the page comes into view, so the last card is current.
  await expect(accountLink).toHaveAttribute('aria-current', 'location')
  await expect(profileLink).not.toHaveAttribute('aria-current')
  await page.screenshot({ path: test.info().outputPath('staff-account-sections.png') })

  // A window where the page does not scroll: the first card stays current, also after a click (the clicked card stays
  // current only from the build of SCR-ACC-01).
  await page.setViewportSize({ width: 1440, height: 2000 })
  await page.goto(`/admin/staff/${staff.id}`)
  await expect(page.getByRole('heading', { level: 1, name: STAFF_NAME })).toBeVisible()
  await accountLink.click()
  await expect(page.getByRole('heading', { level: 2, name: 'Tài khoản' })).toBeFocused()
  await expect(profileLink).toHaveAttribute('aria-current', 'location')

  // Width: one column of at most 1024 px, centred in the page area, also on a 2560 px screen.
  await page.setViewportSize({ width: 2560, height: 1400 })
  const column = page
    .locator('main > div')
    .filter({ has: page.getByRole('heading', { level: 1, name: STAFF_NAME }) })
  const box = await column.boundingBox()
  const main = await page.locator('main').boundingBox()
  expect(box!.width).toBeLessThanOrEqual(1024)
  expect(Math.abs(box!.x - main!.x - (main!.x + main!.width - box!.x - box!.width))).toBeLessThan(2)
  expect(await noSidewaysScroll(page)).toBe(true)
  await page.screenshot({ path: test.info().outputPath('staff-account-wide.png'), fullPage: true })
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

test('the Admin rejects a PENDING account from its notice, then lands on the tab Chờ duyệt (STF-09)', async ({
  page,
}) => {
  const staff = await newPendingAccount('Đặng Thị Từ')
  await signedInAdmin(page)
  await page.goto(`/admin/staff/${staff.id}`)

  const notice = page.getByRole('status').filter({ hasText: 'Tài khoản đang chờ duyệt' })
  await expect(notice).toContainText('Phạm Quốc Bảo tạo lúc')
  await expect(page.getByRole('button', { name: 'Khóa tài khoản' })).toHaveCount(0)
  await notice.getByRole('button', { name: 'Từ chối' }).click()
  await page
    .getByRole('alertdialog', { name: 'Từ chối tài khoản này?' })
    .getByRole('button', { name: 'Từ chối tài khoản' })
    .click()

  await expect(page).toHaveURL('/admin/staff?status=PENDING')
  await expect(page.getByText('Đã từ chối tài khoản Đặng Thị Từ.')).toBeVisible()
})
