import AxeBuilder from '@axe-core/playwright'
import { expect, test, type Page } from '@playwright/test'
import { accounts, meId, newSalesConsultant, PASSWORD } from './support/backend'
import { failOnCspViolations, signIn } from './support/pages'

// The first tier of accessibility (DEC-152 (3), DEC-104 (7)): axe on every screen built so far, against the rules of
// WCAG 2.0, 2.1 and 2.2 at levels A and AA. What axe cannot judge stays with code-reviewer (WCAG 2.2 AA by hand).

failOnCspViolations()

const WCAG_AA = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22a', 'wcag22aa']

/** Fails with the rule, its help and the elements of every violation axe finds on the page as it stands. */
async function expectNoViolations(page: Page) {
  const { violations } = await new AxeBuilder({ page }).withTags(WCAG_AA).analyze()
  const found = violations.map((violation) => ({
    rule: violation.id,
    help: violation.help,
    elements: violation.nodes.map((node) => node.target.join(' ')),
  }))
  expect(found).toEqual([])
}

test('SCR-AUT-01 Sign-in', async ({ page }) => {
  await page.goto('/admin/sign-in')
  await expect(page.getByRole('button', { name: 'Đăng nhập' })).toBeVisible()
  await expectNoViolations(page)
})

test('SCR-AUT-02 Required password change', async ({ page }) => {
  // A new account must change its password first, so its sign-in lands on SCR-AUT-02.
  const initial = `E2e-axe-${Date.now()}`
  const staff = await newSalesConsultant(initial)
  await page.goto('/admin/sign-in')
  await signIn(page, staff.email, initial)
  await expect(page).toHaveURL('/admin/change-password')
  await expect(page.getByLabel('Mật khẩu mới', { exact: true })).toBeVisible()
  await expectNoViolations(page)
})

test.describe('the screens of the admin, signed in as the Admin', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/admin/sign-in')
    await signIn(page, accounts.admin, PASSWORD)
    await expect(page).toHaveURL('/admin/staff')
  })

  test('SCR-STF-01 Staff list', async ({ page }) => {
    await expect(page.getByRole('region', { name: /nhân viên/i })).toBeVisible()
    await expectNoViolations(page)
  })

  test('SCR-STF-02 New staff account', async ({ page }) => {
    await page.goto('/admin/staff/new')
    await expect(page.getByRole('main').getByRole('heading').first()).toBeVisible()
    await expectNoViolations(page)
  })

  test('SCR-STF-03 Staff account', async ({ page }) => {
    await page.goto(`/admin/staff/${await meId(accounts.admin, PASSWORD)}`)
    await expect(page.getByRole('main').getByRole('heading').first()).toBeVisible()
    await expectNoViolations(page)
  })

  test('SCR-ACC-01 My account', async ({ page }) => {
    await page.goto('/admin/account')
    await expect(page.getByRole('main').getByRole('heading').first()).toBeVisible()
    await expectNoViolations(page)
  })

  test('the 404 inside the admin layout', async ({ page }) => {
    await page.goto('/admin/khong-ton-tai')
    await expect(page.getByRole('main')).toBeVisible()
    await expectNoViolations(page)
  })
})
