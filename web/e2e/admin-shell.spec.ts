import { expect, test, type Page } from '@playwright/test'
import { accounts, disableStaff, newSalesConsultant, PASSWORD } from './support/backend'
import { failOnCspViolations, signIn } from './support/pages'

// SCR-AUT-01, SCR-AUT-02 and SCR-ADM-01 (docs/SCREENS.md) against the API, the Firebase Auth Emulator and the web.

failOnCspViolations()

const menu = (page: Page) => page.getByRole('navigation', { name: 'Menu' })
// Inside the page: Next.js also renders an empty role="alert" element that announces route changes.
const alert = (page: Page) => page.getByRole('main').getByRole('alert')

test('the Admin signs in and sees every menu entry', async ({ page }) => {
  await page.goto('/admin/sign-in')
  await signIn(page, accounts.admin, PASSWORD)

  await expect(page).toHaveURL('/admin/staff')
  await expect(menu(page).getByRole('link', { name: 'Nhân viên' })).toBeVisible()
  await expect(menu(page).getByRole('link', { name: 'Địa điểm kinh doanh' })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Chủ cửa hàng' })).toBeVisible()
  await page.screenshot({ path: test.info().outputPath('admin-layout.png'), fullPage: true })
})

test('the Store Manager does not see the settings', async ({ page }) => {
  await page.goto('/admin/sign-in')
  await signIn(page, accounts.manager, PASSWORD)

  await expect(page).toHaveURL('/admin/staff')
  await expect(menu(page).getByRole('link', { name: 'Nhân viên' })).toBeVisible()
  await expect(menu(page).getByText('Cài đặt')).toHaveCount(0)
  await expect(menu(page).getByRole('link', { name: 'Địa điểm kinh doanh' })).toHaveCount(0)
})

test('a new staff member changes the password first, then sees no entry without staff.read', async ({
  page,
}) => {
  const initial = `E2e-initial-${Date.now()}`
  const changed = `E2e-changed-${Date.now()}`
  const staff = await newSalesConsultant(initial)

  await page.goto('/admin/sign-in')
  await signIn(page, staff.email, initial)
  await expect(page).toHaveURL('/admin/change-password')

  // SCR-AUT-02: the web checks the length and the repeat; the API checks the rest of R-USR-11.
  const newPassword = page.getByLabel('Mật khẩu mới', { exact: true })
  const confirmation = page.getByLabel('Nhập lại mật khẩu mới')
  const submit = page.getByRole('button', { name: 'Đổi mật khẩu' })
  await newPassword.fill('ngan')
  await confirmation.fill('ngan')
  await submit.click()
  await expect(page.getByText('Mật khẩu mới dài từ 12 đến 128 ký tự.')).toBeVisible()

  await newPassword.fill(changed)
  await confirmation.fill(`${changed}x`)
  await submit.click()
  await expect(page.getByText('Mật khẩu nhập lại không khớp.')).toBeVisible()

  // A common password of the API's list (R-USR-11): the field shows the API's text (DEC-117 (4)).
  const common = 'matkhaumoi1234'
  await newPassword.fill(common)
  await confirmation.fill(common)
  await submit.click()
  await expect(
    page.getByText('Mật khẩu này quá phổ biến. Vui lòng chọn mật khẩu khác.'),
  ).toBeVisible()
  await expect(newPassword).toHaveValue(common)
  await page.screenshot({ path: test.info().outputPath('change-password.png'), fullPage: true })

  // The API changes it and revokes every session (DEC-094): the user signs in again.
  await newPassword.fill(changed)
  await confirmation.fill(changed)
  await submit.click()
  await expect(page).toHaveURL('/admin/sign-in?reason=password-changed')
  await expect(
    page.getByText('Đã đổi mật khẩu. Vui lòng đăng nhập lại bằng mật khẩu mới.'),
  ).toBeVisible()

  await signIn(page, staff.email, changed)
  await expect(page).toHaveURL('/admin/account')
  await expect(menu(page).getByRole('link')).toHaveCount(0)

  await page.goto('/admin/staff')
  await expect(
    page.getByRole('heading', { name: 'Bạn không có quyền xem trang này.' }),
  ).toBeVisible()
  await expect(page.getByRole('link', { name: 'Tài khoản của tôi' })).toBeVisible()
  await page.screenshot({ path: test.info().outputPath('no-permission.png'), fullPage: true })
})

test('a page opened without a session comes back after the sign-in', async ({ page }) => {
  await page.goto('/admin/settings/business-location')
  await expect(page).toHaveURL('/admin/sign-in?next=%2Fadmin%2Fsettings%2Fbusiness-location')

  await signIn(page, accounts.admin, PASSWORD)
  await expect(page).toHaveURL('/admin/settings/business-location')
  await expect(page.getByRole('heading', { name: 'Địa điểm kinh doanh' })).toBeVisible()
})

test('a disabled account is refused and signed out', async ({ page }) => {
  const password = `E2e-disabled-${Date.now()}`
  const staff = await newSalesConsultant(password)
  await disableStaff(staff.id)

  await page.goto('/admin/sign-in')
  await signIn(page, staff.email, password)

  await expect(alert(page)).toHaveText('Tài khoản đã bị khóa. Vui lòng liên hệ quản lý cửa hàng.')
  await expect(page).toHaveURL('/admin/sign-in')
  await page.goto('/admin/account')
  await expect(page).toHaveURL('/admin/sign-in?next=%2Fadmin%2Faccount')
  await page.screenshot({ path: test.info().outputPath('disabled-account.png') })
})

test('a customer account is not a staff account', async ({ page }) => {
  await page.goto('/admin/sign-in')
  await signIn(page, accounts.customer, PASSWORD)

  await expect(alert(page)).toHaveText('Tài khoản này không phải tài khoản nhân viên.')
  await expect(page).toHaveURL('/admin/sign-in')
})

test('an unknown admin page shows the 404 inside the layout', async ({ page }) => {
  await page.goto('/admin/sign-in')
  await signIn(page, accounts.admin, PASSWORD)
  await expect(page).toHaveURL('/admin/staff')

  await page.goto('/admin/khong-ton-tai')
  await expect(page.getByRole('heading', { name: 'Không tìm thấy trang này.' })).toBeVisible()
  await expect(page).toHaveTitle(/^Không tìm thấy trang này\./)
  await expect(menu(page).getByRole('link', { name: 'Nhân viên' })).toBeVisible()
  await page.getByRole('link', { name: 'Về trang chính' }).click()
  await expect(page).toHaveURL('/admin/staff')
})
