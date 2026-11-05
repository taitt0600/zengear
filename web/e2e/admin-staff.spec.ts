import { expect, test, type Page } from '@playwright/test'
import {
  accounts,
  disableStaff,
  newSalesConsultant,
  PASSWORD,
  staffListPage,
} from './support/backend'
import { failOnCspViolations, signIn } from './support/pages'

// SCR-STF-01 Staff list (docs/SCREENS.md, STF-05) against the API, the Firebase Auth Emulator and the web. The
// development database keeps the accounts of earlier runs: a flow opens the page that holds the row it looks for.

failOnCspViolations()

async function signedInAdmin(page: Page) {
  await page.goto('/admin/sign-in')
  await signIn(page, accounts.admin, PASSWORD)
  await expect(page).toHaveURL('/admin/staff')
}

const table = (page: Page) => page.getByRole('region', { name: 'Bảng nhân viên' })
const searchBox = (page: Page) => page.getByLabel('Lọc theo tên, mã hoặc email')
const statusFilter = (page: Page) => page.getByRole('combobox', { name: 'Trạng thái' })

/** A day as the list shows it: dd/MM/yyyy in Vietnam. */
const shown = (date: Date) =>
  new Intl.DateTimeFormat('vi', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    timeZone: 'Asia/Ho_Chi_Minh',
  }).format(date)

test('the Admin sees the staff accounts and sorts them by name', async ({ page }) => {
  await signedInAdmin(page)
  await expect(
    page.getByRole('heading', { level: 1, name: 'Nhân viên', exact: true }),
  ).toBeVisible()

  const ownerPage = await staffListPage(accounts.admin, 'sort=name')
  if (ownerPage > 1) await page.goto(`/admin/staff?page=${ownerPage}`)
  const owner = table(page).getByRole('row').filter({ hasText: accounts.admin })
  await expect(owner.getByRole('link', { name: 'Chủ cửa hàng' })).toBeVisible()
  await expect(owner.getByText('Đang hoạt động')).toBeVisible()
  await page.screenshot({ path: test.info().outputPath('staff-list.png'), fullPage: true })

  const name = page.getByRole('columnheader', { name: 'Họ tên' })
  await expect(name).toHaveAttribute('aria-sort', 'ascending')
  await name.getByRole('button', { name: 'Họ tên' }).click()
  await expect(page).toHaveURL(/[?&]sort=-name(&|$)/)
  await expect(name).toHaveAttribute('aria-sort', 'descending')
})

test('on a narrow screen the way to add an account takes the full width', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 })
  await signedInAdmin(page)

  const add = page.getByRole('main').getByRole('link', { name: 'Thêm nhân viên' })
  const description = page.getByText(
    'Tài khoản nhân viên của cửa hàng, cùng vai trò và trạng thái.',
    {
      exact: false,
    },
  )
  await expect(add).toBeVisible()
  const [button, text] = [await add.boundingBox(), await description.boundingBox()]
  expect(Math.abs(button!.width - text!.width)).toBeLessThan(2)
  await page.screenshot({ path: test.info().outputPath('staff-list-narrow.png'), fullPage: true })
})

test('a disabled account shows with its status, and the filters clear', async ({ page }) => {
  const staff = await newSalesConsultant(`E2e-list-${Date.now()}`)
  await disableStaff(staff.id)
  const query = 'status=DISABLED&sort=-employeeCode'
  const listPage = await staffListPage(staff.email, query)
  await signedInAdmin(page)
  await page.goto(`/admin/staff?${query}${listPage > 1 ? `&page=${listPage}` : ''}`)

  await expect(statusFilter(page)).toHaveText('Đã khóa')
  await searchBox(page).fill(staff.email)
  const rows = table(page).getByRole('row')
  await expect(rows).toHaveCount(2)
  await expect(rows.nth(1).getByText('Đã khóa')).toBeVisible()
  await expect(rows.nth(1)).toContainText(shown(new Date()))

  await searchBox(page).fill('khong-co-ai@example.com')
  await expect(page.getByText('Không có nhân viên phù hợp với bộ lọc.')).toBeVisible()
  await page.getByRole('button', { name: 'Xóa bộ lọc' }).click()
  await expect(page).not.toHaveURL(/status=/)
  await expect(searchBox(page)).toHaveValue('')
  await expect(statusFilter(page)).toHaveText('Tất cả')
})
