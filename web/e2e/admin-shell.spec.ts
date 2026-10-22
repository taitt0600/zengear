import { expect, test, type Page } from '@playwright/test'
import {
  accounts,
  changePassword,
  disableStaff,
  newSalesConsultant,
  PASSWORD,
} from './support/backend'

// SCR-AUT-01 and SCR-ADM-01 (docs/SCREENS.md) against the API, the Firebase Auth Emulator and the web.

async function signIn(page: Page, email: string, password: string) {
  const button = page.getByRole('button', { name: 'Đăng nhập' })
  await expect(button).toBeEnabled()
  await page.getByLabel('Email công ty').fill(email)
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password)
  await button.click()
}

// Every flow fails on a CSP violation: the CSP lets the pages reach the API and the emulator and run the scripts of
// Next.js (DEC-104 (10), DEC-113 (6)).
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
  await expect(page.getByText('Màn hình đổi mật khẩu sẽ có ở bản sau.')).toBeVisible()

  // SCR-AUT-02 comes with STF-03: the change goes through the API, which revokes every session (DEC-094).
  await changePassword(staff.email, initial, changed)
  await page.getByRole('button', { name: 'Đăng xuất' }).click()
  await expect(page).toHaveURL('/admin/sign-in')

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
