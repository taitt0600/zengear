import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { calls, manager, ok, refused, renderAccount } from './staff-account.fixtures'

// The confirmation "Khôi phục tài khoản?" of SCR-STF-03 (docs/SCREENS.md; R-USR-03, R-USR-12).

const api = vi.hoisted(() => ({ fetch: vi.fn() }))
vi.mock('@/lib/api/api', () => ({ apiFetch: api.fetch }))
vi.mock('@/lib/auth/session', () => ({}))
const toast = vi.hoisted(() => ({ error: vi.fn(), success: vi.fn() }))
vi.mock('sonner', () => ({ toast }))

const disabled = { ...manager, status: 'DISABLED', leftDate: '2026-11-01' }
const GET = `GET /api/v1/admin/staff/${manager.id}`
const RESTORE = `POST /api/v1/admin/staff/${manager.id}/restore`
const confirm = () => screen.getByRole('button', { name: 'Khôi phục' })

async function open() {
  await userEvent.click(await screen.findByRole('button', { name: 'Khôi phục tài khoản' }))
  expect(
    screen.getByRole('alertdialog', { name: 'Khôi phục tài khoản?' }),
  ).toHaveAccessibleDescription(
    'Nhân viên đăng nhập lại được bằng mật khẩu đang có, và ngày nghỉ việc được xóa.',
  )
}

beforeEach(() => {
  vi.resetAllMocks()
  api.fetch.mockResolvedValue(ok(disabled))
})

describe('the confirmation "Khôi phục tài khoản?"', () => {
  it('restores the account, then shows it as the API returns it', async () => {
    renderAccount()
    await open()
    api.fetch.mockResolvedValue(ok(manager))

    await userEvent.click(confirm())

    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument())
    expect(toast.success).toHaveBeenCalledWith('Đã khôi phục tài khoản.')
    expect(calls(api.fetch)).toEqual([GET, RESTORE, GET])
    expect(await screen.findByRole('button', { name: 'Khóa tài khoản' })).toBeVisible()
  })

  it('gives the focus to the status card once a restore has replaced its button', async () => {
    renderAccount()
    await open()
    api.fetch.mockResolvedValue(ok(manager))

    await userEvent.click(confirm())

    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument())
    await waitFor(() =>
      expect(screen.getByRole('heading', { level: 2, name: 'Trạng thái tài khoản' })).toHaveFocus(),
    )
  })

  it('gives the focus back to the button that opened it when nothing changed', async () => {
    renderAccount()
    await open()

    await userEvent.click(screen.getByRole('button', { name: 'Hủy' }))

    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument())
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Khôi phục tài khoản' })).toHaveFocus(),
    )
  })

  it('shows a refusal in a toast and closes, without reloading (R-USR-12)', async () => {
    const detail = 'Chỉ người giữ vai trò Admin mới khôi phục được tài khoản Admin.'
    renderAccount()
    await open()
    api.fetch.mockRejectedValue(
      refused(409, { type: '/problems/admin-account-not-restorable', detail }),
    )

    await userEvent.click(confirm())

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(detail))
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
    expect(calls(api.fetch)).toEqual([GET, RESTORE])
  })

  it('reloads the account after a concurrency conflict', async () => {
    const detail = 'Dữ liệu đã được người khác thay đổi.'
    renderAccount()
    await open()
    api.fetch.mockRejectedValueOnce(
      refused(409, { type: '/problems/concurrency-conflict', detail }),
    )

    await userEvent.click(confirm())

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(detail))
    await waitFor(() => expect(calls(api.fetch)).toEqual([GET, RESTORE, GET]))
  })

  it('gives the focus to the status card when a reload after a refusal replaces its button', async () => {
    renderAccount()
    await open()
    // Someone else restored the account first: the reload, which the confirmation does not wait for and which is held
    // until the test answers it, brings it back ACTIVE, without the button that opened the confirmation.
    let answer: (value: unknown) => void = () => {}
    api.fetch
      .mockRejectedValueOnce(
        refused(409, { type: '/problems/concurrency-conflict', detail: 'Đã thay đổi.' }),
      )
      .mockImplementationOnce(() => new Promise((resolve) => (answer = resolve)))

    await userEvent.click(confirm())

    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument())
    expect(toast.error).toHaveBeenCalledWith('Đã thay đổi.')
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Khôi phục tài khoản' })).toHaveFocus(),
    )
    answer(ok(manager))
    await waitFor(() =>
      expect(screen.getByRole('heading', { level: 2, name: 'Trạng thái tài khoản' })).toHaveFocus(),
    )
  })

  it('shows no answer with the web’s text and reloads', async () => {
    renderAccount()
    await open()
    api.fetch.mockRejectedValueOnce(new TypeError('Failed to fetch'))

    await userEvent.click(confirm())

    await waitFor(() =>
      expect(toast.error).toHaveBeenCalledWith(
        'Không kết nối được máy chủ. Vui lòng kiểm tra mạng và thử lại.',
      ),
    )
    await waitFor(() => expect(calls(api.fetch)).toEqual([GET, RESTORE, GET]))
  })

  it('sends one request on a double click', async () => {
    renderAccount()
    await open()
    api.fetch.mockReturnValue(new Promise(() => {}))

    await userEvent.dblClick(confirm())

    expect(confirm()).toBeDisabled()
    expect(calls(api.fetch).filter((call) => call === RESTORE)).toHaveLength(1)
  })
})
