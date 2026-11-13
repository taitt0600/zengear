import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { StaffListItemDto } from '@/lib/api/generated/model'
import { renderWithProviders } from '@/test/render'
import { body, calls, ok, refused } from './staff-account.fixtures'
import { manager, owner, paged, staffListScreen } from './staff-list.fixtures'

// The menu "Thao tác" of a row of SCR-STF-01 (docs/screens/SCR-STF-01.md, Actions of a row; R-USR-03, R-USR-12): what
// it offers to whom, and the dialogs of SCR-STF-03 it opens, after which the list loads again.

const api = vi.hoisted(() => ({ fetch: vi.fn() }))
vi.mock('@/lib/api/api', () => ({ apiFetch: api.fetch }))
vi.mock('@/lib/auth/session', () => ({}))
const toast = vi.hoisted(() => ({ error: vi.fn(), success: vi.fn() }))
vi.mock('sonner', () => ({ toast }))
// The URL of the page: its search params are what navigation.search holds.
const navigation = vi.hoisted(() => ({ search: '' }))
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  usePathname: () => '/admin/staff',
  useSearchParams: () => new URLSearchParams(navigation.search),
}))

/** An active Sales Consultant, whom the user may lock. */
const seller: StaffListItemDto = {
  id: '0199a0f3-0000-7000-8000-000000000003',
  name: 'Bán hàng',
  email: 'seller@zengear.test',
  employeeCode: 'NV0003',
  joinedDate: '2026-10-15',
  leftDate: null,
  status: 'ACTIVE',
  roles: [{ id: '0199a000-0000-7000-8000-000000000003', name: 'Sales Consultant' }],
}

const LIST = 'GET /api/v1/admin/staff?pageSize=20'
const ALL = ['staff.read', 'staff.disable']

/** The list answers with `rows`; a command answers with `command` (a value, or an error it throws). */
function answer(rows: StaffListItemDto[], command?: unknown) {
  api.fetch.mockImplementation(async (url: string, init?: RequestInit) => {
    if ((init?.method ?? 'GET') === 'GET') return paged(rows)
    if (command instanceof Error) throw command
    return ok(command)
  })
}

const render = (permissions = ALL, meId?: string) =>
  renderWithProviders(staffListScreen(permissions, meId))

const menuButton = (name: string) => screen.findByRole('button', { name: `Thao tác cho ${name}` })

/** Opens the menu of the row of `name`, and gives the names of its items. */
async function openMenu(name: string) {
  await userEvent.click(await menuButton(name))
  const menu = await screen.findByRole('menu')
  return within(menu)
    .getAllByRole('menuitem')
    .map((item) => item.textContent)
}

beforeEach(() => {
  vi.resetAllMocks()
  navigation.search = ''
  answer([owner, seller, manager])
})

describe('the menu of a row', () => {
  it('offers only "Xem chi tiết" without staff.disable, a link to the account', async () => {
    render(['staff.read'])

    expect(await openMenu('Bán hàng')).toEqual(['Xem chi tiết'])
    expect(screen.getByRole('menuitem', { name: 'Xem chi tiết' })).toHaveAttribute(
      'href',
      `/admin/staff/${seller.id}`,
    )
  })

  it('offers no command on the user own account (R-USR-12)', async () => {
    render(ALL, owner.id)

    expect(await openMenu('Chủ cửa hàng')).toEqual(['Xem chi tiết'])
  })

  it('offers to lock an active account and to restore or lock again a disabled one', async () => {
    render()

    expect(await openMenu('Bán hàng')).toEqual(['Xem chi tiết', 'Khóa tài khoản'])
    await userEvent.keyboard('{Escape}')
    expect(await openMenu('Quản lý (fixture)')).toEqual([
      'Xem chi tiết',
      'Khôi phục tài khoản',
      'Khóa lại',
    ])
  })
})

describe('the commands of a row', () => {
  it('locks the account in the dialog of SCR-STF-03, then loads the list again', async () => {
    render()
    await openMenu('Bán hàng')
    await userEvent.click(screen.getByRole('menuitem', { name: 'Khóa tài khoản' }))
    const dialog = screen.getByRole('dialog', { name: 'Khóa tài khoản' })
    expect(within(dialog).getByRole('checkbox', { name: 'Nhân viên nghỉ việc' })).not.toBeChecked()
    answer([owner, { ...seller, status: 'DISABLED' }, manager], { ...seller, status: 'DISABLED' })

    await userEvent.click(within(dialog).getByRole('button', { name: 'Khóa' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(toast.success).toHaveBeenCalledWith('Đã khóa tài khoản.')
    expect(calls(api.fetch)).toEqual([LIST, `POST /api/v1/admin/staff/${seller.id}/disable`, LIST])
    expect(body(api.fetch, 1)).toEqual({ leftDate: null })
    await waitFor(() => expect(screen.getAllByText('Đã khóa')).toHaveLength(2))
  })

  it('opens "Khóa lại" ticked with the left date of the row', async () => {
    render()
    await openMenu('Quản lý (fixture)')
    await userEvent.click(screen.getByRole('menuitem', { name: 'Khóa lại' }))

    const dialog = screen.getByRole('dialog', { name: 'Khóa tài khoản' })
    expect(within(dialog).getByRole('checkbox', { name: 'Nhân viên nghỉ việc' })).toBeChecked()
    expect(within(dialog).getByRole('button', { name: /Ngày nghỉ việc/ })).toHaveTextContent(
      '01/11/2026',
    )
  })

  it('restores the account after its confirmation, then loads the list again', async () => {
    render()
    await openMenu('Quản lý (fixture)')
    await userEvent.click(screen.getByRole('menuitem', { name: 'Khôi phục tài khoản' }))
    answer([owner, seller, { ...manager, status: 'ACTIVE' }], { ...manager, status: 'ACTIVE' })

    await userEvent.click(
      within(screen.getByRole('alertdialog', { name: 'Khôi phục tài khoản?' })).getByRole(
        'button',
        { name: 'Khôi phục' },
      ),
    )

    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument())
    expect(toast.success).toHaveBeenCalledWith('Đã khôi phục tài khoản.')
    expect(calls(api.fetch)).toEqual([LIST, `POST /api/v1/admin/staff/${manager.id}/restore`, LIST])
    await waitFor(() => expect(screen.queryByText('Đã khóa')).toBeNull())
  })

  it('shows a 404 in a toast, closes and loads the list again', async () => {
    const detail = 'Không tìm thấy tài khoản nhân viên.'
    render()
    await openMenu('Quản lý (fixture)')
    await userEvent.click(screen.getByRole('menuitem', { name: 'Khôi phục tài khoản' }))
    answer([owner, seller], refused(404, { type: '/problems/not-found', detail }))

    await userEvent.click(screen.getByRole('button', { name: 'Khôi phục' }))

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(detail))
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
    await waitFor(() => expect(calls(api.fetch)).toHaveLength(3))
    expect(calls(api.fetch)[2]).toBe(LIST)
  })

  it('shows a refusal in a toast and closes, without loading the list again (R-USR-12)', async () => {
    const detail = 'Chỉ người giữ vai trò Admin mới khôi phục được tài khoản Admin.'
    render()
    await openMenu('Quản lý (fixture)')
    await userEvent.click(screen.getByRole('menuitem', { name: 'Khôi phục tài khoản' }))
    answer(
      [owner, seller, manager],
      refused(409, { type: '/problems/admin-account-not-restorable', detail }),
    )

    await userEvent.click(screen.getByRole('button', { name: 'Khôi phục' }))

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(detail))
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
    expect(calls(api.fetch)).toEqual([LIST, `POST /api/v1/admin/staff/${manager.id}/restore`])
  })

  it('gives the focus to the list when the row leaves it with its dialog open', async () => {
    render()
    await openMenu('Bán hàng')
    await userEvent.click(screen.getByRole('menuitem', { name: 'Khóa tài khoản' }))
    // As under the filter "Đang hoạt động": the account locked, the list loads without it.
    answer([owner], { ...seller, status: 'DISABLED' })

    await userEvent.click(screen.getByRole('button', { name: 'Khóa' }))

    await waitFor(() => expect(screen.queryByRole('link', { name: 'Bán hàng' })).toBeNull())
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    await waitFor(() => expect(toast.success).toHaveBeenCalledWith('Đã khóa tài khoản.'))
    expect(screen.getByRole('region', { name: 'Bảng nhân viên' })).toHaveFocus()
  })

  it('gives the focus to "Xóa bộ lọc" when the last row of the filter leaves the list', async () => {
    navigation.search = 'status=ACTIVE'
    answer([seller])
    render()
    await openMenu('Bán hàng')
    await userEvent.click(screen.getByRole('menuitem', { name: 'Khóa tài khoản' }))
    answer([], { ...seller, status: 'DISABLED' })

    await userEvent.click(screen.getByRole('button', { name: 'Khóa' }))

    expect(await screen.findByText('Không có nhân viên phù hợp với bộ lọc.')).toBeVisible()
    await waitFor(() => expect(screen.getByRole('button', { name: 'Xóa bộ lọc' })).toHaveFocus())
  })

  it('gives the focus back to the menu button when the dialog closes', async () => {
    render()
    await openMenu('Bán hàng')
    await userEvent.click(screen.getByRole('menuitem', { name: 'Khóa tài khoản' }))

    await userEvent.click(screen.getByRole('button', { name: 'Hủy' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Thao tác cho Bán hàng' })).toHaveFocus(),
    )
  })
})
