import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { NextIntlClientProvider } from 'next-intl'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '@/lib/api/problems'
import messages from '../../../messages/vi.json'
import { ChangePasswordForm } from './change-password-form'

const api = vi.hoisted(() => ({ changeMyPassword: vi.fn() }))
vi.mock('./me', () => ({ changeMyPassword: api.changeMyPassword }))

const session = vi.hoisted(() => ({ reauthenticate: vi.fn() }))
vi.mock('@/lib/auth/session', () => session)

const toast = vi.hoisted(() => ({ error: vi.fn(), success: vi.fn() }))
vi.mock('sonner', () => ({ toast }))

const problem = (status: number, name: string, errors?: Record<string, string[]>) =>
  new ApiError(status, { type: `/problems/${name}`, status, errors })
const recentLogin = () => problem(403, 'recent-login-required')
const firebaseError = (code: string) => Object.assign(new Error(code), { code })

const NEW_PASSWORD = 'mot mat khau moi dai'
const EMAIL = 'tuvan@zengear.test'
const onChanged = vi.fn()

function renderForm(onCancel?: () => void) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <NextIntlClientProvider locale="vi" messages={messages} timeZone="Asia/Ho_Chi_Minh">
      <QueryClientProvider client={client}>
        <ChangePasswordForm email={EMAIL} onChanged={onChanged} onCancel={onCancel} />
      </QueryClientProvider>
    </NextIntlClientProvider>,
  )
  return client
}

const usernameOf = (element: ParentNode) => element.querySelector('input[autocomplete="username"]')

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
    const username = usernameOf(document)
    expect(username).toHaveValue(EMAIL)
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

  it('400: shows the refusal under the field, in Vietnamese, and keeps what was typed', async () => {
    api.changeMyPassword.mockRejectedValue(
      problem(400, 'validation', { newPassword: ['New password is too common.'] }),
    )
    renderForm()
    await submit()

    expect(
      await screen.findByText('Không dùng được mật khẩu này. Vui lòng chọn mật khẩu khác.'),
    ).toBeInTheDocument()
    expect(screen.queryByText('New password is too common.')).not.toBeInTheDocument()
    expect(screen.getByLabelText('Mật khẩu mới')).toHaveValue(NEW_PASSWORD)
    expect(screen.getByLabelText('Mật khẩu mới')).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByLabelText('Mật khẩu mới')).toHaveFocus()
    expect(onChanged).not.toHaveBeenCalled()
  })

  it('keeps no password in the mutation cache after an answer', async () => {
    api.changeMyPassword.mockRejectedValue(problem(409, 'staff-account-required'))
    const client = renderForm()
    await submit()

    await waitFor(() => expect(toast.error).toHaveBeenCalled())
    const sent = client.getMutationCache().getAll()
    expect(sent).not.toHaveLength(0)
    for (const mutation of sent) expect(mutation.state.variables).toBeUndefined()
  })

  it('409 staff-account-required: shows its message', async () => {
    api.changeMyPassword.mockRejectedValue(problem(409, 'staff-account-required'))
    renderForm()
    await submit()

    await waitFor(() =>
      expect(toast.error).toHaveBeenCalledWith('Chỉ tài khoản nhân viên mới đổi mật khẩu tại đây.'),
    )
    expect(onChanged).not.toHaveBeenCalled()
  })

  it('another problem: shows its detail, never its ruleId', async () => {
    api.changeMyPassword.mockRejectedValue(
      new ApiError(500, { type: '/problems/x', status: 500, detail: 'Chi tiết.', ruleId: 'R-X' }),
    )
    renderForm()
    await submit()

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('Chi tiết.'))
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
    expect(usernameOf(dialog)).toHaveValue(EMAIL)
    await user.type(within(dialog).getByLabelText('Mật khẩu hiện tại'), 'mat-khau-cu')
    await user.click(within(dialog).getByRole('button', { name: 'Tiếp tục' }))

    await waitFor(() => expect(onChanged).toHaveBeenCalledOnce())
    expect(session.reauthenticate).toHaveBeenCalledExactlyOnceWith('mat-khau-cu')
    expect(api.changeMyPassword).toHaveBeenCalledTimes(2)
    expect(api.changeMyPassword).toHaveBeenLastCalledWith(NEW_PASSWORD)
  })

  it('maps the answers of the second send as the first', async () => {
    api.changeMyPassword
      .mockRejectedValueOnce(recentLogin())
      .mockRejectedValueOnce(
        problem(400, 'validation', { newPassword: ['New password is too common.'] }),
      )
    renderForm()
    const user = await submit()

    const dialog = await reauthDialog()
    await user.type(within(dialog).getByLabelText('Mật khẩu hiện tại'), 'mat-khau-cu')
    await user.click(within(dialog).getByRole('button', { name: 'Tiếp tục' }))

    expect(
      await screen.findByText('Không dùng được mật khẩu này. Vui lòng chọn mật khẩu khác.'),
    ).toBeInTheDocument()
    expect(onChanged).not.toHaveBeenCalled()
  })

  it('sends the change only once more: a second 403 is an error', async () => {
    api.changeMyPassword.mockRejectedValue(recentLogin())
    renderForm()
    const user = await submit()

    const dialog = await reauthDialog()
    await user.type(within(dialog).getByLabelText('Mật khẩu hiện tại'), 'mat-khau-cu')
    await user.click(within(dialog).getByRole('button', { name: 'Tiếp tục' }))

    await waitFor(() =>
      expect(toast.error).toHaveBeenCalledWith('Đã có lỗi xảy ra. Vui lòng thử lại.'),
    )
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
