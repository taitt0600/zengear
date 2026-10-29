import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { CurrentUserProvider, type LoadedCurrentUser } from '@/features/identity/access'
import { ApiError } from '@/lib/api/problems'
import { renderWithProviders } from '@/test/render'
import { AccountLayout } from './account-layout'
import { AccountPasswordSection } from './account-password-section'
import { AccountProfileSection } from './account-profile-section'

const api = vi.hoisted(() => ({ me: vi.fn(), updateMe: vi.fn(), changeMyPassword: vi.fn() }))
// The files the sections import: GET /me of the session, and the calls of this module.
vi.mock('@/features/identity/access/api/me', () => ({
  meQuery: { queryKey: ['me'], queryFn: () => api.me() },
}))
vi.mock('../api/me', () => ({ updateMe: api.updateMe, changeMyPassword: api.changeMyPassword }))

const session = vi.hoisted(() => ({ reauthenticate: vi.fn() }))
vi.mock('@/lib/auth/session', () => session)

const toast = vi.hoisted(() => ({ error: vi.fn(), success: vi.fn() }))
vi.mock('sonner', () => ({ toast }))

const consultant: LoadedCurrentUser = {
  id: '0199a0f3-0000-7000-8000-000000000009',
  name: 'Tư vấn bán hàng',
  email: 'tuvan@zengear.test',
  isStaff: true,
  mustChangePassword: false,
  permissions: [],
  etag: '"v1"',
}
const signOut = vi.fn()

const validation = (...errors: { pointer?: string; code: string; detail: string }[]) =>
  new ApiError(400, {
    type: '/problems/validation',
    status: 400,
    detail: 'Vui lòng kiểm tra lại các thông tin đã nhập.',
    errors,
  })

// The screen as its page composes it.
const renderScreen = () =>
  renderWithProviders(
    <CurrentUserProvider value={{ me: consultant, signOut }}>
      <AccountLayout
        title="Tài khoản của tôi"
        profile={<AccountProfileSection />}
        password={<AccountPasswordSection />}
      />
    </CurrentUserProvider>,
  )

const nameField = () => screen.getByLabelText('Tên hiển thị')

async function rename(name: string) {
  const user = userEvent.setup()
  const field = await screen.findByLabelText('Tên hiển thị')
  await user.clear(field)
  if (name) await user.type(field, name)
  return user
}

beforeEach(() => {
  vi.resetAllMocks()
  api.me.mockResolvedValue(consultant)
  api.updateMe.mockImplementation(async (name: string) => ({ ...consultant, name, etag: '"v2"' }))
  signOut.mockResolvedValue(undefined)
})

describe('the account', () => {
  it('shows the display name, and the email read-only with its note', async () => {
    renderScreen()

    expect(await screen.findByLabelText('Tên hiển thị')).toHaveValue('Tư vấn bán hàng')
    const email = screen.getByLabelText('Email')
    expect(email).toHaveValue('tuvan@zengear.test')
    expect(email).toHaveAttribute('readonly')
    expect(email).toHaveAccessibleDescription(
      'Email là tên đăng nhập. Liên hệ quản trị viên khi cần đổi.',
    )
  })

  it('requires the display name before sending', async () => {
    renderScreen()
    const user = await rename('')
    await user.click(screen.getByRole('button', { name: 'Lưu' }))

    expect(await screen.findByText('Vui lòng nhập tên hiển thị.')).toBeInTheDocument()
    expect(api.updateMe).not.toHaveBeenCalled()
  })
})

describe('"Lưu" (PATCH /me with If-Match)', () => {
  it('sends the trimmed name with the loaded ETag, then says it is saved and reloads GET /me', async () => {
    renderScreen()
    const user = await rename('  Tên mới  ')
    api.me.mockResolvedValue({ ...consultant, name: 'Tên mới', etag: '"v2"' })
    await user.click(screen.getByRole('button', { name: 'Lưu' }))

    await waitFor(() => expect(toast.success).toHaveBeenCalledWith('Đã lưu.'))
    expect(api.updateMe).toHaveBeenCalledExactlyOnceWith('Tên mới', '"v1"')
    expect(api.me).toHaveBeenCalledTimes(2)
    expect(nameField()).toHaveValue('Tên mới')

    // The next save sends the version that GET /me returned after this one.
    await user.click(screen.getByRole('button', { name: 'Lưu' }))
    await waitFor(() => expect(api.updateMe).toHaveBeenLastCalledWith('Tên mới', '"v2"'))
  })

  it('holds the name and the version of the PATCH answer, even when GET /me then fails', async () => {
    renderScreen()
    const user = await rename('Tên mới')
    api.me.mockRejectedValue(new TypeError('Failed to fetch'))
    await user.click(screen.getByRole('button', { name: 'Lưu' }))

    await waitFor(() => expect(toast.success).toHaveBeenCalledWith('Đã lưu.'))
    expect(nameField()).toHaveValue('Tên mới')
    await user.click(screen.getByRole('button', { name: 'Lưu' }))
    await waitFor(() => expect(api.updateMe).toHaveBeenLastCalledWith('Tên mới', '"v2"'))
  })

  it('keeps the version it loaded when GET /me refetches in the background', async () => {
    const client = renderScreen()
    const user = await rename('Tên đang gõ')
    api.me.mockResolvedValue({ ...consultant, name: 'Đổi ở nơi khác', etag: '"v2"' })
    await client.refetchQueries({ queryKey: ['me'] })

    expect(nameField()).toHaveValue('Tên đang gõ')
    await user.click(screen.getByRole('button', { name: 'Lưu' }))
    await waitFor(() => expect(api.updateMe).toHaveBeenCalledWith('Tên đang gõ', '"v1"'))
  })

  it("400: shows the API's text under the field and keeps the name", async () => {
    api.updateMe.mockRejectedValue(
      validation({ pointer: '#/name', code: 'NAME_TOO_LONG', detail: 'Tên tối đa 100 ký tự.' }),
    )
    renderScreen()
    const user = await rename('Tên mới')
    await user.click(screen.getByRole('button', { name: 'Lưu' }))

    expect(await screen.findByText('Tên tối đa 100 ký tự.')).toBeInTheDocument()
    expect(nameField()).toHaveValue('Tên mới')
    expect(nameField()).toHaveAttribute('aria-invalid', 'true')
    expect(nameField()).toHaveFocus()
  })

  it('400: shows the texts no field shows at the top of the form', async () => {
    api.updateMe.mockRejectedValue(
      validation(
        { pointer: '#/name', code: 'NAME_REQUIRED', detail: 'Vui lòng nhập tên.' },
        { pointer: '#', code: 'BODY_INVALID', detail: 'Nội dung yêu cầu không đọc được.' },
      ),
    )
    renderScreen()
    const user = await rename('Tên mới')
    await user.click(screen.getByRole('button', { name: 'Lưu' }))

    const text = await screen.findByText('Nội dung yêu cầu không đọc được.')
    // The field's own error is an alert too: the one at the top of the form holds the text.
    expect(screen.getAllByRole('alert').find((alert) => alert.contains(text))).toBeDefined()
    expect(screen.getByText('Vui lòng nhập tên.')).toBeInTheDocument()
    // The field in error takes the focus, not the alert.
    await waitFor(() => expect(screen.getByLabelText('Tên hiển thị')).toHaveFocus())
    expect(toast.error).not.toHaveBeenCalled()
  })

  it.each([
    [412, 'precondition-failed', 'Dữ liệu đã được người khác thay đổi. Đang tải lại.'],
    [428, 'if-match-required', 'Không xác định được phiên bản của dữ liệu đang sửa. Đang tải lại.'],
  ])("%i: shows the API's text and reloads the form", async (status, type, detail) => {
    api.updateMe.mockRejectedValue(
      new ApiError(status, { type: `/problems/${type}`, status, detail }),
    )
    renderScreen()
    const user = await rename('Tên của tôi')
    api.me.mockResolvedValue({ ...consultant, name: 'Đổi ở nơi khác', etag: '"v2"' })
    await user.click(screen.getByRole('button', { name: 'Lưu' }))

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(detail))
    await waitFor(() => expect(nameField()).toHaveValue('Đổi ở nơi khác'))

    api.updateMe.mockResolvedValue({ ...consultant, etag: '"v3"' })
    await rename('Tên sau khi tải lại')
    await user.click(screen.getByRole('button', { name: 'Lưu' }))
    await waitFor(() =>
      expect(api.updateMe).toHaveBeenLastCalledWith('Tên sau khi tải lại', '"v2"'),
    )
  })

  it('412 and GET /me then fails: keeps what was typed and says the reload failed', async () => {
    api.updateMe.mockRejectedValue(
      new ApiError(412, {
        type: '/problems/precondition-failed',
        status: 412,
        detail: 'Dữ liệu đã được người khác thay đổi. Đang tải lại.',
      }),
    )
    renderScreen()
    const user = await rename('Tên của tôi')
    api.me.mockRejectedValue(new TypeError('Failed to fetch'))
    await user.click(screen.getByRole('button', { name: 'Lưu' }))

    await waitFor(() => expect(toast.error).toHaveBeenCalledTimes(2))
    expect(toast.error).toHaveBeenNthCalledWith(
      1,
      'Dữ liệu đã được người khác thay đổi. Đang tải lại.',
    )
    expect(toast.error).toHaveBeenNthCalledWith(
      2,
      'Không kết nối được máy chủ. Vui lòng kiểm tra mạng và thử lại.',
    )
    expect(nameField()).toHaveValue('Tên của tôi')
  })

  it('cannot save without a version', async () => {
    api.me.mockResolvedValue({ ...consultant, etag: undefined })
    renderScreen()

    expect(await screen.findByRole('button', { name: 'Lưu' })).toBeDisabled()
  })
})

describe('"Đổi mật khẩu"', () => {
  it('opens the form of SCR-AUT-02 in a dialog, and signs out after the change', async () => {
    api.changeMyPassword.mockResolvedValue(undefined)
    renderScreen()
    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'Đổi mật khẩu' }))

    const dialog = await screen.findByRole('dialog', { name: 'Đổi mật khẩu' })
    await user.type(within(dialog).getByLabelText('Mật khẩu mới'), 'mot mat khau moi dai')
    await user.type(within(dialog).getByLabelText('Nhập lại mật khẩu mới'), 'mot mat khau moi dai')
    await user.click(within(dialog).getByRole('button', { name: 'Đổi mật khẩu' }))

    await waitFor(() => expect(signOut).toHaveBeenCalledExactlyOnceWith('password-changed'))
  })

  it('closes with "Hủy"', async () => {
    renderScreen()
    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'Đổi mật khẩu' }))
    const dialog = await screen.findByRole('dialog', { name: 'Đổi mật khẩu' })
    await user.click(within(dialog).getByRole('button', { name: 'Hủy' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(api.changeMyPassword).not.toHaveBeenCalled()
  })

  it('stays open while the change is sent, so its answer is shown', async () => {
    api.changeMyPassword.mockReturnValue(new Promise(() => {}))
    renderScreen()
    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'Đổi mật khẩu' }))
    const dialog = await screen.findByRole('dialog', { name: 'Đổi mật khẩu' })
    await user.type(within(dialog).getByLabelText('Mật khẩu mới'), 'mot mat khau moi dai')
    await user.type(within(dialog).getByLabelText('Nhập lại mật khẩu mới'), 'mot mat khau moi dai')
    await user.click(within(dialog).getByRole('button', { name: 'Đổi mật khẩu' }))
    await waitFor(() => expect(api.changeMyPassword).toHaveBeenCalledOnce())

    await user.keyboard('{Escape}')
    expect(screen.getByRole('dialog', { name: 'Đổi mật khẩu' })).toBeInTheDocument()
    expect(within(dialog).getByRole('button', { name: 'Hủy' })).toBeDisabled()
  })

  it('names the account for password managers', async () => {
    renderScreen()
    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'Đổi mật khẩu' }))
    const dialog = await screen.findByRole('dialog', { name: 'Đổi mật khẩu' })

    expect(within(dialog).getByDisplayValue('tuvan@zengear.test')).toHaveAttribute(
      'autocomplete',
      'username',
    )
  })

  it('returns to the password form when the nested sign-in dialog closes', async () => {
    api.changeMyPassword.mockRejectedValue(
      new ApiError(403, { type: '/problems/recent-login-required', status: 403 }),
    )
    renderScreen()
    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'Đổi mật khẩu' }))
    const dialog = await screen.findByRole('dialog', { name: 'Đổi mật khẩu' })
    await user.type(within(dialog).getByLabelText('Mật khẩu mới'), 'mot mat khau moi dai')
    await user.type(within(dialog).getByLabelText('Nhập lại mật khẩu mới'), 'mot mat khau moi dai')
    await user.click(within(dialog).getByRole('button', { name: 'Đổi mật khẩu' }))

    const reauth = await screen.findByRole('dialog', {
      name: 'Vui lòng nhập lại mật khẩu hiện tại để tiếp tục',
    })
    await user.click(within(reauth).getByRole('button', { name: 'Hủy' }))

    await waitFor(() =>
      expect(
        screen.queryByRole('dialog', { name: 'Vui lòng nhập lại mật khẩu hiện tại để tiếp tục' }),
      ).not.toBeInTheDocument(),
    )
    const form = screen.getByRole('dialog', { name: 'Đổi mật khẩu' })
    await waitFor(() => expect(form).toContainElement(document.activeElement as HTMLElement))
  })
})
