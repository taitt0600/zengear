import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '@/lib/api/problems'
import { consultant, renderAccountScreen, validation } from './account.fixtures'

// The card "Mật khẩu" of SCR-ACC-01 (docs/screens/SCR-ACC-01.md, Password card): the password changed on the page,
// the current password checked by Firebase first (DEC-094), then POST /me/change-password, the checklist of R-USR-11.

const api = vi.hoisted(() => ({ me: vi.fn(), updateMe: vi.fn(), changeMyPassword: vi.fn() }))
vi.mock('@/features/identity/access/api/me', () => ({
  meQuery: { queryKey: ['me'], queryFn: () => api.me(), meta: { reportErrors: false } },
}))
vi.mock('../api/me', () => ({
  updateMe: api.updateMe,
  changeMyPassword: api.changeMyPassword,
  changePasswordKey: ['postApiV1AdminMeChangePassword'],
}))

const session = vi.hoisted(() => ({ reauthenticate: vi.fn() }))
vi.mock('@/lib/auth/session', () => session)

const toast = vi.hoisted(() => ({ error: vi.fn(), success: vi.fn() }))
vi.mock('sonner', () => ({ toast }))

const signOut = vi.fn()
const firebaseError = (code: string) => Object.assign(new Error(code), { code })
const NEW_PASSWORD = 'mot mat khau moi dai'

const card = () => screen.getByRole('region', { name: 'Mật khẩu' })
const current = () => within(card()).getByLabelText(/^Mật khẩu hiện tại/)
const newPassword = () => within(card()).getByLabelText(/^Mật khẩu mới/)
const repeat = () => within(card()).getByLabelText(/^Nhập lại mật khẩu mới/)
const submit = () => within(card()).getByRole('button', { name: 'Đổi mật khẩu' })

/** Renders the page and fills the card; an empty value leaves its field empty. */
async function fill(currentValue: string, next = NEW_PASSWORD, again = next) {
  renderAccountScreen(signOut)
  const user = userEvent.setup()
  await screen.findByText('Tư vấn bán hàng · tuvan@zengear.test')
  if (currentValue) await user.type(current(), currentValue)
  if (next) await user.type(newPassword(), next)
  if (again) await user.type(repeat(), again)
  return user
}

beforeEach(() => {
  vi.resetAllMocks()
  api.me.mockResolvedValue(consultant)
  session.reauthenticate.mockResolvedValue(undefined)
  api.changeMyPassword.mockResolvedValue(undefined)
  signOut.mockResolvedValue(undefined)
})

describe('the card "Mật khẩu"', () => {
  it('is a region reached by #password, with its note, the required note and no dialog', async () => {
    renderAccountScreen(signOut)

    expect(card()).toHaveAttribute('id', 'password')
    expect(
      within(card()).getByText(
        'Sau khi đổi, bạn và mọi thiết bị đã đăng nhập phải đăng nhập lại bằng mật khẩu mới.',
      ),
    ).toBeInTheDocument()
    expect(within(card()).getByText(/là bắt buộc\./)).toBeInTheDocument()
    expect(await within(card()).findByLabelText(/^Mật khẩu hiện tại/)).toBeInTheDocument()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('asks for the three passwords, each required, and names the account for password managers', async () => {
    renderAccountScreen(signOut)
    await screen.findByText('Tư vấn bán hàng · tuvan@zengear.test')

    expect(current()).toBeRequired()
    expect(current()).toHaveAttribute('autocomplete', 'current-password')
    expect(newPassword()).toBeRequired()
    expect(newPassword()).toHaveAttribute('autocomplete', 'new-password')
    expect(repeat()).toBeRequired()
    expect(within(card()).getByDisplayValue('tuvan@zengear.test')).toHaveAttribute(
      'autocomplete',
      'username',
    )
  })
})

describe('the checklist of R-USR-11', () => {
  it('describes each field by its items, unmet while nothing is typed', async () => {
    renderAccountScreen(signOut)
    await screen.findByText('Tư vấn bán hàng · tuvan@zengear.test')

    expect(newPassword()).toHaveAccessibleDescription(
      'Từ 12 đến 128 ký tự (chưa đạt) Không chứa chữ zengear (chưa đạt) Không phải mật khẩu mặc định hay mật khẩu phổ biến (kiểm khi đổi)',
    )
    expect(repeat()).toHaveAccessibleDescription('Giống mật khẩu mới (chưa đạt)')
    expect(
      within(card()).getByRole('heading', { name: 'Yêu cầu của mật khẩu mới' }),
    ).toBeInTheDocument()
  })

  it('follows what is typed', async () => {
    await fill('', 'matkhaucuahang', 'matkhau')

    expect(newPassword()).toHaveAccessibleDescription(
      'Từ 12 đến 128 ký tự (đạt) Không chứa chữ zengear (đạt) Không phải mật khẩu mặc định hay mật khẩu phổ biến (kiểm khi đổi)',
    )
    expect(repeat()).toHaveAccessibleDescription('Giống mật khẩu mới (chưa đạt)')
  })

  it('does not block a password that contains zengear: the API refuses it', async () => {
    api.changeMyPassword.mockRejectedValue(
      validation({
        pointer: '#/newPassword',
        code: 'PASSWORD_CONTAINS_BRAND',
        detail: 'Mật khẩu không được chứa chữ zengear.',
      }),
    )
    const user = await fill('mat-khau-cu', 'cua hang zengear moi')
    await user.click(submit())

    expect(await screen.findByText('Mật khẩu không được chứa chữ zengear.')).toBeInTheDocument()
    expect(newPassword()).toHaveFocus()
    expect(api.changeMyPassword).toHaveBeenCalledOnce()
  })

  it('blocks a short password and a repeat that differs', async () => {
    const user = await fill('mat-khau-cu', 'ngan', 'khac')
    await user.click(submit())

    expect(await screen.findByText('Mật khẩu mới dài từ 12 đến 128 ký tự.')).toBeInTheDocument()
    expect(screen.getByText('Mật khẩu nhập lại không khớp.')).toBeInTheDocument()
    expect(session.reauthenticate).not.toHaveBeenCalled()
  })
})

describe('"Đổi mật khẩu"', () => {
  it('signs in again with the current password, sends the new one, then signs out', async () => {
    const user = await fill('mat-khau-cu')
    await user.click(submit())

    await waitFor(() => expect(signOut).toHaveBeenCalledExactlyOnceWith('password-changed'))
    expect(session.reauthenticate).toHaveBeenCalledExactlyOnceWith('mat-khau-cu')
    expect(api.changeMyPassword).toHaveBeenCalledExactlyOnceWith(NEW_PASSWORD)
    expect(session.reauthenticate.mock.invocationCallOrder[0]).toBeLessThan(
      api.changeMyPassword.mock.invocationCallOrder[0],
    )
    // The passwords leave the form, and the button stays disabled until the page changes.
    expect(current()).toHaveValue('')
    expect(submit()).toBeDisabled()
  })

  it('requires the current password', async () => {
    const user = await fill('')
    await user.click(submit())

    expect(await screen.findByText('Vui lòng nhập mật khẩu hiện tại.')).toBeInTheDocument()
    expect(session.reauthenticate).not.toHaveBeenCalled()
  })

  it('shows a wrong current password under its field, focused, and sends nothing', async () => {
    session.reauthenticate.mockRejectedValue(firebaseError('auth/invalid-credential'))
    const user = await fill('sai-mat-khau')
    await user.click(submit())

    expect(await screen.findByText('Mật khẩu hiện tại không đúng.')).toBeInTheDocument()
    expect(current()).toHaveAttribute('aria-invalid', 'true')
    expect(current()).toHaveFocus()
    expect(api.changeMyPassword).not.toHaveBeenCalled()
  })

  it('shows the other answers of Firebase at the top of the form', async () => {
    session.reauthenticate.mockRejectedValue(firebaseError('auth/too-many-requests'))
    const user = await fill('mat-khau-cu')
    await user.click(submit())

    const alert = await within(card()).findByText('Bạn đã thử quá nhiều lần. Vui lòng thử lại sau.')
    expect(
      within(card())
        .getAllByRole('alert')
        .some((item) => item.contains(alert)),
    ).toBe(true)
    expect(api.changeMyPassword).not.toHaveBeenCalled()
  })

  it('403 recent-login-required: its text in a toast, no dialog, no retry', async () => {
    const detail =
      'Lần đăng nhập gần nhất đã quá 5 phút, nên cần xác nhận lại trước khi đổi mật khẩu.'
    api.changeMyPassword.mockRejectedValue(
      new ApiError(403, { type: '/problems/recent-login-required', status: 403, detail }),
    )
    const user = await fill('mat-khau-cu')
    await user.click(submit())

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(detail))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(api.changeMyPassword).toHaveBeenCalledOnce()
    expect(signOut).not.toHaveBeenCalled()
  })
})
