import { expect, test, type Page } from '@playwright/test'
import {
  accounts,
  disableStaff,
  meId,
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

/** Whether the page fits the window's width, with nothing to scroll sideways and nothing scrolled. */
const noSidewaysScroll = (page: Page) =>
  page.evaluate(
    () => document.documentElement.scrollWidth <= window.innerWidth && window.scrollX === 0,
  )

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

test('the Admin locks and restores an account from its row, and has no command on their own', async ({
  page,
}) => {
  // A long email, so that the nine columns are wider than the card: the table's box scrolls sideways, the page never
  // (the Layout), also when the focus reaches the menu button at the end of the row.
  const staff = await newSalesConsultant(`E2e-row-${Date.now()}`, `e2e${'x'.repeat(40)}`)
  const listPage = await staffListPage(staff.email, 'sort=name')
  await signedInAdmin(page)
  if (listPage > 1) await page.goto(`/admin/staff?page=${listPage}`)

  const row = table(page).getByRole('row').filter({ hasText: staff.email })
  const menuOf = (target: typeof row) => target.getByRole('button', { name: /^Thao tác cho / })
  await expect(row.getByText('Đang hoạt động')).toBeVisible()
  expect(await noSidewaysScroll(page)).toBe(true)
  await menuOf(row).click()
  const menu = page.getByRole('menu')
  await expect(menu.getByRole('menuitem')).toHaveText(['Xem chi tiết', 'Khóa tài khoản'])
  expect(await noSidewaysScroll(page)).toBe(true)
  // The screenshot shows the destructive item under the pointer: its text on its red ground (globals.test.ts).
  await menu.getByRole('menuitem', { name: 'Khóa tài khoản' }).hover()
  await page.screenshot({
    path: test.info().outputPath('staff-list-row-menu.png'),
    animations: 'disabled',
  })
  await menu.getByRole('menuitem', { name: 'Khóa tài khoản' }).click()
  const dialog = page.getByRole('dialog', { name: 'Khóa tài khoản' })
  await dialog.getByRole('button', { name: 'Khóa', exact: true }).click()
  await expect(page.getByText('Đã khóa tài khoản.')).toBeVisible()
  await expect(dialog).toBeHidden()
  await expect(row.getByText('Đã khóa', { exact: true })).toBeVisible()

  // A click after the dialog: the page takes the pointer again (no layer of the menu left over it).
  await menuOf(row).click()
  await expect(menu.getByRole('menuitem')).toHaveText([
    'Xem chi tiết',
    'Khôi phục tài khoản',
    'Khóa lại',
  ])
  await menu.getByRole('menuitem', { name: 'Khôi phục tài khoản' }).click()
  await page
    .getByRole('alertdialog')
    .getByRole('button', { name: 'Khôi phục', exact: true })
    .click()
  await expect(page.getByText('Đã khôi phục tài khoản.')).toBeVisible()
  await expect(row.getByText('Đang hoạt động')).toBeVisible()

  const ownerPage = await staffListPage(accounts.admin, 'sort=name')
  await page.goto(`/admin/staff${ownerPage > 1 ? `?page=${ownerPage}` : ''}`)
  const owner = table(page).getByRole('row').filter({ hasText: accounts.admin })
  await menuOf(owner).click()
  await expect(menu.getByRole('menuitem')).toHaveText(['Xem chi tiết'])
  await menu.getByRole('menuitem', { name: 'Xem chi tiết' }).click()
  await expect(page).toHaveURL(new RegExp(`/admin/staff/${await meId(accounts.admin, PASSWORD)}$`))
})
