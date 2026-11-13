import { act, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { body, calls, manager, ok, refused, renderAccount } from './staff-account.fixtures'

// The dialog "Khóa tài khoản" of SCR-STF-03 (docs/screens/SCR-STF-03.md; R-USR-07, R-USR-12, R-SEC-04): what it sends,
// and where each answer of the API shows.

const api = vi.hoisted(() => ({ fetch: vi.fn() }))
vi.mock('@/lib/api/api', () => ({ apiFetch: api.fetch }))
vi.mock('@/lib/auth/session', () => ({}))
const toast = vi.hoisted(() => ({ error: vi.fn(), success: vi.fn() }))
vi.mock('sonner', () => ({ toast }))
// Today in Vietnam, fixed: the dialog picks it and the calendar disables the days after it.
vi.mock('../utils/staff-account', async (original) => ({
  ...(await original<typeof import('../utils/staff-account')>()),
  todayInVietnam: () => '2026-11-05',
}))

const GET = `GET /api/v1/admin/staff/${manager.id}`
const DISABLE = `POST /api/v1/admin/staff/${manager.id}/disable`
const dialog = () => screen.getByRole('dialog', { name: 'Khóa tài khoản' })
const leaving = () => within(dialog()).getByRole('checkbox', { name: 'Nhân viên nghỉ việc' })
const lock = () => within(dialog()).getByRole('button', { name: 'Khóa' })

/** Opens the dialog from the status card, by the button `name`. */
async function open(name = 'Khóa tài khoản') {
  await userEvent.click(await screen.findByRole('button', { name }))
  return dialog()
}

beforeEach(() => {
  vi.resetAllMocks()
  api.fetch.mockResolvedValue(ok(manager))
})

describe('the dialog "Khóa tài khoản"', () => {
  it('locks without a left date, then shows the account as the API returns it', async () => {
    renderAccount()
    await open()
    expect(within(dialog()).getByRole('alert')).toHaveTextContent(
      'Nhân viên sẽ bị đăng xuất khỏi mọi thiết bị ngay lập tức.',
    )
    expect(leaving()).not.toBeChecked()
    api.fetch.mockResolvedValue(ok({ ...manager, status: 'DISABLED' }))

    await userEvent.click(lock())

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(toast.success).toHaveBeenCalledWith('Đã khóa tài khoản.')
    expect(calls(api.fetch)).toEqual([GET, DISABLE, GET])
    expect(body(api.fetch, 1)).toEqual({ leftDate: null })
    expect(await screen.findByRole('button', { name: 'Khôi phục tài khoản' })).toBeVisible()
  })

  it('gives the focus to the status card once a lock has replaced its button', async () => {
    renderAccount()
    await open()
    api.fetch.mockResolvedValue(ok({ ...manager, status: 'DISABLED' }))

    await userEvent.click(lock())

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    await waitFor(() =>
      expect(screen.getByRole('heading', { level: 2, name: 'Trạng thái tài khoản' })).toHaveFocus(),
    )
  })

  it('gives the focus back to the button that opened it when nothing changed', async () => {
    renderAccount()
    await open()

    await userEvent.click(within(dialog()).getByRole('button', { name: 'Hủy' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Khóa tài khoản' })).toHaveFocus(),
    )
  })

  it('gives the focus back to "Khóa lại", which stays after it locks again', async () => {
    const disabled = { ...manager, status: 'DISABLED', leftDate: '2026-11-01' } as const
    api.fetch.mockResolvedValue(ok(disabled))
    renderAccount()
    await open('Khóa lại')

    await userEvent.click(lock())

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    await waitFor(() => expect(screen.getByRole('button', { name: 'Khóa lại' })).toHaveFocus())
  })

  it('records the last working day, today by default, which it sends', async () => {
    renderAccount()
    await open()

    await userEvent.click(leaving())
    const date = within(dialog()).getByRole('button', { name: /Ngày nghỉ việc/ })
    expect(date).toHaveTextContent('05/11/2026')
    expect(date).toHaveAccessibleDescription(
      'Ngày làm việc cuối cùng: không sau hôm nay và không trước ngày vào làm (01/10/2026).',
    )
    await userEvent.click(lock())

    await waitFor(() => expect(calls(api.fetch)).toContain(DISABLE))
    expect(body(api.fetch, 1)).toEqual({ leftDate: '2026-11-05' })
  })

  it('offers no day after today in the calendar, whose texts are Vietnamese', async () => {
    renderAccount()
    await open()
    await userEvent.click(leaving())

    await userEvent.click(within(dialog()).getByRole('button', { name: /Ngày nghỉ việc/ }))
    const calendar = await screen.findByRole('grid')
    expect(
      within(calendar).getByRole('button', { name: /ngày 6 tháng 11 năm 2026/ }),
    ).toBeDisabled()
    expect(
      within(calendar).getByRole('button', {
        name: 'Thứ Năm, ngày 5 tháng 11 năm 2026, hôm nay, đã chọn',
      }),
    ).toBeEnabled()
    expect(calendar).toHaveAccessibleName('Tháng Mười Một 2026')
    expect(screen.getByRole('button', { name: 'Tháng trước' })).toBeVisible()
    expect(screen.getByRole('button', { name: 'Tháng sau' })).toHaveAttribute(
      'aria-disabled',
      'true',
    )
  })

  it('opens the calendar on the chosen day, with no tooltip on a month button that cannot move', async () => {
    renderAccount()
    await open()
    await userEvent.click(leaving())

    await userEvent.click(within(dialog()).getByRole('button', { name: /Ngày nghỉ việc/ }))
    const calendar = await screen.findByRole('grid')

    await waitFor(() =>
      expect(within(calendar).getByRole('button', { name: /hôm nay, đã chọn/ })).toHaveFocus(),
    )
    act(() => screen.getByRole('button', { name: 'Tháng sau' }).focus())
    expect(screen.queryByRole('tooltip')).not.toBeInTheDocument()
  })

  it('keeps the focus on "Tháng trước" when it reaches the month of the joined date', async () => {
    renderAccount()
    await open()
    await userEvent.click(leaving())
    await userEvent.click(within(dialog()).getByRole('button', { name: /Ngày nghỉ việc/ }))
    await screen.findByRole('grid')

    act(() => screen.getByRole('button', { name: 'Tháng trước' }).focus())
    expect(await screen.findByRole('tooltip')).toHaveTextContent('Tháng trước')
    await userEvent.keyboard('{Enter}')

    expect(await screen.findByRole('grid')).toHaveAccessibleName('Tháng Mười 2026')
    expect(screen.getByRole('button', { name: 'Tháng trước' })).toHaveAttribute(
      'aria-disabled',
      'true',
    )
    expect(screen.getByRole('button', { name: 'Tháng trước' })).toHaveFocus()
  })

  it('opens "Khóa lại" with the left date already recorded', async () => {
    api.fetch.mockResolvedValue(ok({ ...manager, status: 'DISABLED', leftDate: '2026-11-01' }))
    renderAccount()
    await open('Khóa lại')

    expect(leaving()).toBeChecked()
    expect(within(dialog()).getByRole('button', { name: /Ngày nghỉ việc/ })).toHaveTextContent(
      '01/11/2026',
    )
  })

  it('cannot record a left date for a person who joins after today', async () => {
    api.fetch.mockResolvedValue(ok({ ...manager, joinedDate: '2026-12-01' }))
    renderAccount()
    await open()

    expect(leaving()).toBeDisabled()
    expect(
      within(dialog()).getByText('Ngày vào làm sau hôm nay, nên chưa ghi được ngày nghỉ việc.'),
    ).toBeVisible()
  })

  it('shows a refusal of the left date under the field and stays open (R-USR-07)', async () => {
    const detail = 'Ngày nghỉ việc không được trước ngày vào làm.'
    renderAccount()
    await open()
    await userEvent.click(leaving())
    api.fetch.mockRejectedValue(
      refused(409, {
        type: '/problems/left-date-before-joined-date',
        detail,
        errors: [{ pointer: '#/leftDate', code: 'LEFT_DATE_BEFORE_JOINED_DATE', detail }],
      }),
    )

    await userEvent.click(lock())

    expect(await within(dialog()).findByText(detail)).toBeVisible()
    expect(within(dialog()).getByRole('button', { name: /Ngày nghỉ việc/ })).toHaveFocus()
    expect(toast.error).not.toHaveBeenCalled()
  })

  it('shows a refusal of the left date at the top while the date is not shown', async () => {
    const detail = 'Ngày nghỉ việc là bắt buộc.'
    renderAccount()
    await open()
    api.fetch.mockRejectedValue(
      refused(400, { errors: [{ pointer: '#/leftDate', code: 'LEFT_DATE_REQUIRED', detail }] }),
    )

    await userEvent.click(lock())

    expect(await within(dialog()).findByText(detail)).toBeVisible()
  })

  it('reloads the account by the address the page was opened with', async () => {
    const id = manager.id.toUpperCase()
    renderAccount(id)
    await open()
    api.fetch.mockResolvedValueOnce(ok({ ...manager, status: 'DISABLED' }))

    await userEvent.click(lock())

    await waitFor(() =>
      expect(calls(api.fetch)).toEqual([
        `GET /api/v1/admin/staff/${id}`,
        `POST /api/v1/admin/staff/${id}/disable`,
        `GET /api/v1/admin/staff/${id}`,
      ]),
    )
  })

  it('shows an item no field takes at the top of the dialog', async () => {
    renderAccount()
    await open()
    api.fetch.mockRejectedValue(
      refused(400, {
        errors: [{ pointer: '', code: 'BODY_INVALID', detail: 'Nội dung yêu cầu không hợp lệ.' }],
      }),
    )

    await userEvent.click(lock())

    expect(await within(dialog()).findByText('Nội dung yêu cầu không hợp lệ.')).toBeVisible()
  })

  it('shows any other refusal in a toast and closes, without reloading (R-TX-11, R-USR-12)', async () => {
    const detail = 'Bạn không được khóa tài khoản giữ vai trò Admin.'
    renderAccount()
    await open()
    api.fetch.mockRejectedValue(
      refused(409, { type: '/problems/admin-account-not-lockable', detail }),
    )

    await userEvent.click(lock())

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(detail))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(calls(api.fetch)).toEqual([GET, DISABLE])
  })

  it('shows a 500 with the web’s text, closes and reloads the account (R-SEC-04)', async () => {
    renderAccount()
    await open()
    api.fetch.mockRejectedValueOnce(refused(500))

    await userEvent.click(lock())

    await waitFor(() =>
      expect(toast.error).toHaveBeenCalledWith('Đã có lỗi xảy ra. Vui lòng thử lại.'),
    )
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    await waitFor(() => expect(calls(api.fetch)).toEqual([GET, DISABLE, GET]))
  })

  it('leaves a 401 to the session rules: no toast', async () => {
    renderAccount()
    await open()
    api.fetch.mockRejectedValueOnce(refused(401))

    await userEvent.click(lock())

    await waitFor(() => expect(calls(api.fetch)).toContain(DISABLE))
    expect(toast.error).not.toHaveBeenCalled()
  })

  it('leaves a session Firebase ended to the session rules: no toast, no reload', async () => {
    renderAccount()
    await open()
    api.fetch.mockRejectedValueOnce(
      Object.assign(new Error('token expired'), { code: 'auth/user-token-expired' }),
    )

    await userEvent.click(lock())

    await waitFor(() => expect(lock()).toBeEnabled())
    expect(calls(api.fetch)).toEqual([GET, DISABLE])
    expect(toast.error).not.toHaveBeenCalled()
  })

  it('sends one request on a double click', async () => {
    renderAccount()
    await open()
    api.fetch.mockReturnValue(new Promise(() => {}))

    await userEvent.dblClick(lock())

    expect(lock()).toBeDisabled()
    expect(calls(api.fetch).filter((call) => call === DISABLE)).toHaveLength(1)
  })
})
