import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '@/lib/api/problems'
import { consultant, renderAccountScreen, validation } from './account.fixtures'

// SCR-ACC-01 My account (docs/screens/SCR-ACC-01.md, its Layout): the heading, the section navigation and the card
// "Thông tin tài khoản" with "Lưu". The card "Mật khẩu" is tested in account-password.test.tsx.

const api = vi.hoisted(() => ({ me: vi.fn(), updateMe: vi.fn(), changeMyPassword: vi.fn() }))
// The files the sections import: GET /me of the session, and the calls of this module.
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
const renderScreen = () => renderAccountScreen(signOut)

const nameField = () => screen.getByLabelText(/^Tên hiển thị/)

async function rename(name: string) {
  const user = userEvent.setup()
  const field = await screen.findByLabelText(/^Tên hiển thị/)
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

describe('the page', () => {
  it('is headed "Tài khoản của tôi", with the name and email of the account', async () => {
    renderScreen()

    expect(screen.getByRole('heading', { level: 1, name: 'Tài khoản của tôi' })).toBeInTheDocument()
    expect(await screen.findByText('Tư vấn bán hàng · tuvan@zengear.test')).toBeInTheDocument()
  })

  it('shows the name alone when the account has no email', async () => {
    api.me.mockResolvedValue({ ...consultant, email: null })
    renderScreen()

    expect(await screen.findByText('Tư vấn bán hàng')).toBeInTheDocument()
    // The Email row says it is empty, as the rows of gap 11 will.
    const email = screen.getByText('Email', { selector: 'dt' })
    expect(email.nextElementSibling).toHaveTextContent('—')
  })

  it('lists its two cards in the section navigation, the first one current', () => {
    renderScreen()

    const nav = screen.getByRole('navigation', { name: 'Trên trang này' })
    const links = within(nav).getAllByRole('link')
    expect(links.map((link) => [link.textContent, link.getAttribute('href')])).toEqual([
      ['Thông tin tài khoản', '#profile'],
      ['Mật khẩu', '#password'],
    ])
    expect(links[0]).toHaveAttribute('aria-current', 'location')
  })

  it('shows both cards with their titles and required notes while GET /me loads', () => {
    api.me.mockReturnValue(new Promise(() => {}))
    renderScreen()

    for (const name of ['Thông tin tài khoản', 'Mật khẩu']) {
      const card = screen.getByRole('region', { name })
      expect(within(card).getByRole('heading', { level: 2, name })).toHaveAttribute(
        'tabindex',
        '-1',
      )
      expect(within(card).getByText(/là bắt buộc\./)).toBeInTheDocument()
    }
    expect(screen.queryByLabelText(/^Tên hiển thị/)).not.toBeInTheDocument()
  })
})

describe('the card "Thông tin tài khoản"', () => {
  it('is a region reached by #profile, with its description and the required note', async () => {
    renderScreen()

    const card = screen.getByRole('region', { name: 'Thông tin tài khoản' })
    expect(card).toHaveAttribute('id', 'profile')
    expect(
      within(card).getByText('Thông tin tài khoản của bạn. Bạn tự sửa được tên hiển thị.'),
    ).toBeInTheDocument()
    expect(within(card).getByText(/^Trường có dấu/)).toHaveTextContent(
      'Trường có dấu *sao là bắt buộc.',
    )
    expect(await within(card).findByLabelText(/^Tên hiển thị/)).toBeInTheDocument()
  })

  it('shows the display name, required, with its hint', async () => {
    renderScreen()

    const field = await screen.findByLabelText(/^Tên hiển thị/)
    expect(field).toHaveValue('Tư vấn bán hàng')
    expect(field).toBeRequired()
    expect(field).toHaveAccessibleDescription('Tối đa 100 ký tự.')
  })

  it('shows the email as a read-only row, without a note', async () => {
    renderScreen()

    expect(await screen.findByRole('definition', { name: 'Email' })).toHaveTextContent(
      'tuvan@zengear.test',
    )
    expect(screen.queryByRole('textbox', { name: 'Email' })).not.toBeInTheDocument()
    expect(screen.queryByText(/Email là tên đăng nhập/)).not.toBeInTheDocument()
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
    const { client } = renderScreen()
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
    await waitFor(() => expect(screen.getByLabelText(/^Tên hiển thị/)).toHaveFocus())
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
