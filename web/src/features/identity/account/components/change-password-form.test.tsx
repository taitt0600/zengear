import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '@/lib/api/problems'
import { renderWithProviders } from '@/test/render'
import { ChangePasswordForm } from './change-password-form'

const api = vi.hoisted(() => ({ changeMyPassword: vi.fn() }))
vi.mock('../api/me', () => ({ changeMyPassword: api.changeMyPassword }))
// The session module the form reads loads GET /me, and with it the API client: never reached here.
vi.mock('@/features/identity/access/api/me', () => ({ meQuery: {} }))

const session = vi.hoisted(() => ({ reauthenticate: vi.fn() }))
vi.mock('@/lib/auth/session', () => session)

const toast = vi.hoisted(() => ({ error: vi.fn(), success: vi.fn() }))
vi.mock('sonner', () => ({ toast }))

const problem = (status: number, name: string, detail?: string) =>
  new ApiError(status, { type: `/problems/${name}`, status, detail })
const validation = (...errors: { pointer?: string; code: string; detail: string }[]) =>
  new ApiError(400, {
    type: '/problems/validation',
    status: 400,
    detail: 'Vui lòng kiểm tra lại các thông tin đã nhập.',
    errors,
  })
const TOO_COMMON = 'Mật khẩu này quá phổ biến. Vui lòng chọn mật khẩu khác.'
const tooCommon = () =>
  validation({ pointer: '#/newPassword', code: 'PASSWORD_TOO_COMMON', detail: TOO_COMMON })
const RECENT_LOGIN =
  'Lần đăng nhập gần nhất đã quá 5 phút, nên cần xác nhận lại trước khi đổi mật khẩu.'
const recentLogin = () => problem(403, 'recent-login-required', RECENT_LOGIN)
const STAFF_ONLY = 'Chỉ tài khoản nhân viên mới đổi mật khẩu tại đây.'
const firebaseError = (code: string) => Object.assign(new Error(code), { code })

const NEW_PASSWORD = 'mot mat khau moi dai'
const EMAIL = 'tuvan@zengear.test'
const onChanged = vi.fn()

const renderForm = (onCancel?: () => void) =>
  renderWithProviders(
    <ChangePasswordForm email={EMAIL} onChanged={onChanged} onCancel={onCancel} />,
  )

// The account for password managers: a hidden field, which has no role or label.
const usernameIn = (element: HTMLElement) => within(element).getByDisplayValue(EMAIL)

async function submit(password = NEW_PASSWORD, confirmation = password) {
  const user = userEvent.setup()
  if (password) await user.type(screen.getByLabelText('Mật khẩu mới'), password)
  if (confirmation) await user.type(screen.getByLabelText('Nhập lại mật khẩu mới'), confirmation)
  await user.click(screen.getByRole('button', { name: 'Đổi mật khẩu' }))
  return user
}

const reauthDialog = () =>
  screen.findByRole('dialog', { name: 'Vui lòng nhập lại mật khẩu hiện tại để tiếp tục' })

beforeEach(() => {
  vi.resetAllMocks()
  api.changeMyPassword.mockResolvedValue(undefined)
  session.reauthenticate.mockResolvedValue(undefined)
  onChanged.mockResolvedValue(undefined)
})

describe('the fields', () => {
  it('describes the new password with the help text', () => {
    renderForm()
    expect(screen.getByLabelText('Mật khẩu mới')).toHaveAccessibleDescription(
      'Tối thiểu 12 ký tự. Không dùng mật khẩu mặc định, mật khẩu phổ biến hay chữ zengear.',
    )
  })

  it('names the account for password managers, out of sight', () => {
    renderForm()
    const username = usernameIn(document.body)
    expect(username).toHaveAttribute('autocomplete', 'username')
    expect(username).not.toBeVisible()
  })

  it('checks the length and the repeat before sending anything', async () => {
    renderForm()
    await submit('ngan', 'khac')

    expect(await screen.findByText('Mật khẩu mới dài từ 12 đến 128 ký tự.')).toBeInTheDocument()
    expect(screen.getByLabelText('Mật khẩu mới')).toHaveAttribute('aria-invalid', 'true')
    expect(api.changeMyPassword).not.toHaveBeenCalled()
  })

  it('refuses a repeat that differs', async () => {
    renderForm()
    await submit(NEW_PASSWORD, `${NEW_PASSWORD}x`)

    expect(await screen.findByText('Mật khẩu nhập lại không khớp.')).toBeInTheDocument()
    expect(api.changeMyPassword).not.toHaveBeenCalled()
  })
})

describe('the answers of POST /me/change-password', () => {
  it('204: sends the new password alone once, then hands over', async () => {
    renderForm()
    await submit()

    await waitFor(() => expect(onChanged).toHaveBeenCalledOnce())
    expect(api.changeMyPassword).toHaveBeenCalledExactlyOnceWith(NEW_PASSWORD)
  })

  it('disables the button while the change is sent, so a double click sends one request', async () => {
    api.changeMyPassword.mockReturnValue(new Promise(() => {}))
    renderForm()
    const user = await submit()

    const button = screen.getByRole('button', { name: 'Đổi mật khẩu' })
    await waitFor(() => expect(button).toBeDisabled())
    await user.dblClick(button)
    expect(api.changeMyPassword).toHaveBeenCalledOnce()
  })

  it("400: shows the API's text under the field and keeps what was typed", async () => {
    api.changeMyPassword.mockRejectedValue(tooCommon())
    renderForm()
    await submit()

    expect(await screen.findByText(TOO_COMMON)).toBeInTheDocument()
    expect(screen.getByLabelText('Mật khẩu mới')).toHaveValue(NEW_PASSWORD)
    expect(screen.getByLabelText('Mật khẩu mới')).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByLabelText('Mật khẩu mới')).toHaveFocus()
    expect(onChanged).not.toHaveBeenCalled()
  })

  it('400: shows the texts no field shows at the top of the form, until the next send', async () => {
    api.changeMyPassword
      .mockRejectedValueOnce(
        validation({
          pointer: '#',
          code: 'BODY_INVALID',
          detail: 'Nội dung yêu cầu không đọc được.',
        }),
      )
      .mockReturnValueOnce(new Promise(() => {}))
    renderForm()
    const user = await submit()

    const text = await screen.findByText('Nội dung yêu cầu không đọc được.')
    const alert = screen.getAllByRole('alert').find((element) => element.contains(text))
    expect(alert).toBeDefined()
    // No field shows an error, so the alert takes the focus.
    await waitFor(() => expect(alert).toHaveFocus())
    expect(screen.getByLabelText('Mật khẩu mới')).toHaveAttribute('aria-invalid', 'false')
    expect(toast.error).not.toHaveBeenCalled()

    await user.click(screen.getByRole('button', { name: 'Đổi mật khẩu' }))
    await waitFor(() =>
      expect(screen.queryByText('Nội dung yêu cầu không đọc được.')).not.toBeInTheDocument(),
    )
  })

  it('keeps no password in the mutation cache after an answer', async () => {
    api.changeMyPassword.mockRejectedValue(problem(409, 'staff-account-required', STAFF_ONLY))
    const client = renderForm()
    await submit()

    await waitFor(() => expect(toast.error).toHaveBeenCalled())
    const sent = client.getMutationCache().getAll()
    expect(sent).not.toHaveLength(0)
    for (const mutation of sent) expect(mutation.state.variables).toBeUndefined()
  })

  it("409 staff-account-required: shows the API's text", async () => {
    api.changeMyPassword.mockRejectedValue(problem(409, 'staff-account-required', STAFF_ONLY))
    renderForm()
    await submit()

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(STAFF_ONLY))
    expect(onChanged).not.toHaveBeenCalled()
  })

  it('another problem: shows its detail, never its ruleId', async () => {
    api.changeMyPassword.mockRejectedValue(
      new ApiError(409, { type: '/problems/x', status: 409, detail: 'Chi tiết.', ruleId: 'R-X' }),
    )
    renderForm()
    await submit()

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('Chi tiết.'))
  })

  it('500 (no detail): shows the generic text', async () => {
    api.changeMyPassword.mockRejectedValue(
      new ApiError(500, { type: '/problems/internal-error', status: 500, title: 'Lỗi hệ thống' }),
    )
    renderForm()
    await submit()

    await waitFor(() =>
      expect(toast.error).toHaveBeenCalledWith('Đã có lỗi xảy ra. Vui lòng thử lại.'),
    )
  })

  it('a network failure: says so', async () => {
    api.changeMyPassword.mockRejectedValue(new TypeError('Failed to fetch'))
    renderForm()
    await submit()

    await waitFor(() =>
      expect(toast.error).toHaveBeenCalledWith(
        'Không kết nối được máy chủ. Vui lòng kiểm tra mạng và thử lại.',
      ),
    )
  })

  it('a session answer (401): leaves it to the session, shows nothing', async () => {
    api.changeMyPassword.mockRejectedValue(new ApiError(401))
    renderForm()
    await submit()

    await waitFor(() => expect(api.changeMyPassword).toHaveBeenCalled())
    expect(toast.error).not.toHaveBeenCalled()
    expect(onChanged).not.toHaveBeenCalled()
  })
})

describe('403 recent-login-required (DEC-094)', () => {
  it('asks for the current password, signs in again, then sends the change once more', async () => {
    api.changeMyPassword.mockRejectedValueOnce(recentLogin()).mockResolvedValueOnce(undefined)
    renderForm()
    const user = await submit()

    const dialog = await reauthDialog()
    expect(usernameIn(dialog)).toHaveAttribute('autocomplete', 'username')
    await user.type(within(dialog).getByLabelText('Mật khẩu hiện tại'), 'mat-khau-cu')
    await user.click(within(dialog).getByRole('button', { name: 'Tiếp tục' }))

    await waitFor(() => expect(onChanged).toHaveBeenCalledOnce())
    expect(session.reauthenticate).toHaveBeenCalledExactlyOnceWith('mat-khau-cu')
    expect(api.changeMyPassword).toHaveBeenCalledTimes(2)
    expect(api.changeMyPassword).toHaveBeenLastCalledWith(NEW_PASSWORD)
  })

  it('maps the answers of the second send as the first', async () => {
    api.changeMyPassword.mockRejectedValueOnce(recentLogin()).mockRejectedValueOnce(tooCommon())
    renderForm()
    const user = await submit()

    const dialog = await reauthDialog()
    await user.type(within(dialog).getByLabelText('Mật khẩu hiện tại'), 'mat-khau-cu')
    await user.click(within(dialog).getByRole('button', { name: 'Tiếp tục' }))

    expect(await screen.findByText(TOO_COMMON)).toBeInTheDocument()
    expect(onChanged).not.toHaveBeenCalled()
  })

  it("sends the change only once more: a second 403 shows the API's text", async () => {
    api.changeMyPassword.mockRejectedValue(recentLogin())
    renderForm()
    const user = await submit()

    const dialog = await reauthDialog()
    await user.type(within(dialog).getByLabelText('Mật khẩu hiện tại'), 'mat-khau-cu')
    await user.click(within(dialog).getByRole('button', { name: 'Tiếp tục' }))

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(RECENT_LOGIN))
    expect(api.changeMyPassword).toHaveBeenCalledTimes(2)
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('a wrong current password: says so in the dialog and sends nothing', async () => {
    api.changeMyPassword.mockRejectedValueOnce(recentLogin())
    session.reauthenticate.mockRejectedValue(firebaseError('auth/invalid-credential'))
    renderForm()
    const user = await submit()

    const dialog = await reauthDialog()
    await user.type(within(dialog).getByLabelText('Mật khẩu hiện tại'), 'sai')
    await user.click(within(dialog).getByRole('button', { name: 'Tiếp tục' }))

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      'Mật khẩu hiện tại không đúng.',
    )
    expect(api.changeMyPassword).toHaveBeenCalledOnce()
  })

  it('cancel while Firebase checks the password: the change is not sent', async () => {
    api.changeMyPassword.mockRejectedValueOnce(recentLogin())
    let accept = () => {}
    session.reauthenticate.mockReturnValue(new Promise<void>((resolve) => (accept = resolve)))
    renderForm()
    const user = await submit()

    const dialog = await reauthDialog()
    await user.type(within(dialog).getByLabelText('Mật khẩu hiện tại'), 'mat-khau-cu')
    await user.click(within(dialog).getByRole('button', { name: 'Tiếp tục' }))
    await waitFor(() => expect(session.reauthenticate).toHaveBeenCalledOnce())
    await user.click(within(dialog).getByRole('button', { name: 'Hủy' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    accept()

    await new Promise((resolve) => setTimeout(resolve, 50))
    expect(api.changeMyPassword).toHaveBeenCalledOnce()
    expect(onChanged).not.toHaveBeenCalled()
  })

  it('cancel closes the dialog, sends nothing and clears the current password', async () => {
    api.changeMyPassword.mockRejectedValueOnce(recentLogin()).mockRejectedValueOnce(recentLogin())
    renderForm()
    const user = await submit()

    let dialog = await reauthDialog()
    await user.type(within(dialog).getByLabelText('Mật khẩu hiện tại'), 'mat-khau-cu')
    await user.click(within(dialog).getByRole('button', { name: 'Hủy' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(session.reauthenticate).not.toHaveBeenCalled()
    expect(api.changeMyPassword).toHaveBeenCalledOnce()

    await user.click(screen.getByRole('button', { name: 'Đổi mật khẩu' }))
    dialog = await reauthDialog()
    expect(within(dialog).getByLabelText('Mật khẩu hiện tại')).toHaveValue('')
  })
})

describe('in a dialog', () => {
  it('offers "Hủy" when the caller can close it', async () => {
    const onCancel = vi.fn()
    renderForm(onCancel)
    await userEvent.setup().click(screen.getByRole('button', { name: 'Hủy' }))
    expect(onCancel).toHaveBeenCalledOnce()
  })

  it('has no "Hủy" on its own page', () => {
    renderForm()
    expect(screen.queryByRole('button', { name: 'Hủy' })).not.toBeInTheDocument()
  })
})
