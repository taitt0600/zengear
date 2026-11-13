import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ADMIN_ID, calls, manager, ok, refused, renderAccount } from './staff-account.fixtures'

// SCR-STF-03 Staff account (docs/screens/SCR-STF-03.md): what the page shows of an account, by its status and the
// user's rights.

const api = vi.hoisted(() => ({ fetch: vi.fn() }))
vi.mock('@/lib/api/api', () => ({ apiFetch: api.fetch }))
vi.mock('@/lib/auth/session', () => ({}))
const toast = vi.hoisted(() => ({ error: vi.fn(), success: vi.fn() }))
vi.mock('sonner', () => ({ toast }))

const card = (name: string) =>
  screen.getByRole('heading', { level: 2, name }).closest('[data-slot=card]') as HTMLElement
const details = () => card('Thông tin nhân viên')
const status = () => card('Trạng thái tài khoản')

beforeEach(() => {
  vi.resetAllMocks()
  api.fetch.mockResolvedValue(ok(manager))
})

describe('the staff account page', () => {
  it('shows the person, their status and the details of the account', async () => {
    renderAccount()

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Quản lý (fixture)' }),
    ).toBeVisible()
    expect(screen.getByText('manager.fixture@zengear.test · FX0001')).toBeVisible()
    expect(screen.getByText('QF')).toBeInTheDocument()
    const fields = within(details())
    expect(fields.getByText('Email').nextSibling).toHaveTextContent('manager.fixture@zengear.test')
    expect(fields.getByText('Mã nhân viên').nextSibling).toHaveTextContent('FX0001')
    expect(fields.getByText('SĐT công việc').nextSibling).toHaveTextContent('—')
    expect(fields.getByText('Vai trò').nextSibling).toHaveTextContent('Store Manager')
    expect(fields.getByText('Ngày vào làm').nextSibling).toHaveTextContent('01/10/2026')
    expect(fields.getByText('Ngày nghỉ việc').nextSibling).toHaveTextContent('—')
    expect(within(status()).getByText('Đang hoạt động')).toBeVisible()
    // The password row shows only while the default password is unchanged.
    expect(within(status()).queryByText('Mật khẩu')).not.toBeInTheDocument()
    expect(within(status()).getByRole('button', { name: 'Khóa tài khoản' })).toBeEnabled()
    expect(calls(api.fetch)).toEqual([`GET /api/v1/admin/staff/${manager.id}`])
  })

  it('says when the default password is unchanged', async () => {
    api.fetch.mockResolvedValue(ok({ ...manager, mustChangePassword: true }))
    renderAccount()

    const row = within(await screen.findByText('Mật khẩu').then((label) => label.parentElement!))
    expect(row.getByText('Chưa đổi mật khẩu mặc định')).toBeVisible()
  })

  it('shows skeletons until the account is loaded', async () => {
    api.fetch.mockReturnValue(new Promise(() => {}))
    renderAccount()

    expect(screen.queryByRole('heading', { level: 1 })).not.toBeInTheDocument()
    expect(document.querySelectorAll('[data-slot=skeleton]').length).toBeGreaterThan(6)
  })

  it('shows the API’s text of a 404 as the page heading, with the way back to the list', async () => {
    api.fetch.mockRejectedValue(
      refused(404, { type: '/problems/not-found', detail: 'Không tìm thấy dữ liệu yêu cầu.' }),
    )
    renderAccount()

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Không tìm thấy dữ liệu yêu cầu.' }),
    ).toBeVisible()
    expect(screen.getByRole('link', { name: 'Về danh sách nhân viên' })).toHaveAttribute(
      'href',
      '/admin/staff',
    )
    expect(screen.queryByRole('heading', { level: 2 })).not.toBeInTheDocument()
  })

  it('shows a failed load with "Thử lại", which loads again', async () => {
    api.fetch.mockRejectedValueOnce(refused(500)).mockResolvedValue(ok(manager))
    renderAccount()

    const alert = await screen.findByRole('alert')
    expect(within(alert).getByRole('heading', { level: 1 })).toHaveTextContent(
      'Đã có lỗi xảy ra. Vui lòng thử lại.',
    )
    await userEvent.click(screen.getByRole('button', { name: 'Thử lại' }))
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Quản lý (fixture)' }),
    ).toBeVisible()
  })

  it('offers no action without staff.disable', async () => {
    renderAccount(manager.id, ['staff.read'])

    await screen.findByRole('heading', { level: 1, name: 'Quản lý (fixture)' })
    expect(within(status()).queryByRole('button')).not.toBeInTheDocument()
  })

  it('offers no action on the user’s own account (R-USR-12)', async () => {
    api.fetch.mockResolvedValue(ok({ ...manager, id: ADMIN_ID }))
    renderAccount(ADMIN_ID)

    expect(await screen.findByText('Bạn không thể khóa tài khoản của chính mình.')).toBeVisible()
    expect(within(status()).queryByRole('button')).not.toBeInTheDocument()
  })

  it('offers no action on an anonymized account (R-USR-03)', async () => {
    api.fetch.mockResolvedValue(ok({ ...manager, status: 'ANONYMIZED' }))
    renderAccount()

    expect(
      await screen.findByText(
        'Dữ liệu cá nhân của tài khoản này đã được ẩn danh, nên không khóa hay khôi phục được nữa.',
      ),
    ).toBeVisible()
    expect(within(status()).getByText('Đã ẩn danh')).toHaveAttribute('data-variant', 'outline')
    expect(within(status()).queryByRole('button')).not.toBeInTheDocument()
  })

  it('offers to restore a disabled account, or to lock it again with why', async () => {
    api.fetch.mockResolvedValue(ok({ ...manager, status: 'DISABLED', leftDate: '2026-11-01' }))
    renderAccount()

    await waitFor(() =>
      expect(within(status()).getByText('Đã khóa')).toHaveAttribute('data-variant', 'secondary'),
    )
    expect(within(details()).getByText('Ngày nghỉ việc').nextSibling).toHaveTextContent(
      '01/11/2026',
    )
    expect(within(status()).getByRole('button', { name: 'Khôi phục tài khoản' })).toBeEnabled()
    expect(within(status()).getByRole('button', { name: 'Khóa lại' })).toHaveAccessibleDescription(
      'Gửi lại lệnh khóa để đăng xuất nhân viên khỏi mọi thiết bị, khi lần khóa trước báo lỗi.',
    )
  })
})
