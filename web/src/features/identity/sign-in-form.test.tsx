import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { NextIntlClientProvider } from 'next-intl'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '@/lib/api/problems'
import messages from '../../../messages/vi.json'
import type { CurrentUser } from './me'
import { SignInForm } from './sign-in-form'

const navigation = vi.hoisted(() => ({
  replace: vi.fn(),
  search: new URLSearchParams(),
}))
vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace: navigation.replace }),
  useSearchParams: () => navigation.search,
}))

const session = vi.hoisted(() => ({
  hasSession: vi.fn(),
  signInWithPassword: vi.fn(),
  signOutAndClear: vi.fn(),
}))
vi.mock('@/lib/auth/session', () => session)

const api = vi.hoisted(() => ({ me: vi.fn() }))
vi.mock('./me', () => ({ meQuery: { queryKey: ['me'], queryFn: () => api.me() } }))

const staff: CurrentUser = {
  id: '0199a0f3-0000-7000-8000-000000000001',
  name: 'Quản lý (fixture)',
  email: 'manager.fixture@zengear.test',
  isStaff: true,
  mustChangePassword: false,
  permissions: ['staff.read'],
}

const firebaseError = (code: string) => Object.assign(new Error(code), { code })
const problem = (status: number, name: string) =>
  new ApiError(status, { type: `/problems/${name}`, status })

function renderForm(search = '') {
  navigation.search = new URLSearchParams(search)
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <NextIntlClientProvider locale="vi" messages={messages} timeZone="Asia/Ho_Chi_Minh">
      <QueryClientProvider client={client}>
        <SignInForm />
      </QueryClientProvider>
    </NextIntlClientProvider>,
  )
}

async function signIn(email = 'nhanvien@zengear.test', password = 'mat-khau-cua-toi') {
  const user = userEvent.setup()
  const button = screen.getByRole('button', { name: 'Đăng nhập' })
  await waitFor(() => expect(button).toBeEnabled())
  if (email) await user.type(screen.getByLabelText('Email công ty'), email)
  if (password) await user.type(screen.getByLabelText('Mật khẩu'), password)
  await user.click(button)
  return user
}

beforeEach(() => {
  vi.resetAllMocks()
  session.hasSession.mockResolvedValue(false)
  session.signInWithPassword.mockResolvedValue(undefined)
  session.signOutAndClear.mockResolvedValue(undefined)
  api.me.mockResolvedValue(staff)
})

describe('fields', () => {
  it('requires the email and the password', async () => {
    renderForm()
    await signIn('', '')

    expect(await screen.findByText('Vui lòng nhập email công ty.')).toBeInTheDocument()
    expect(screen.getByText('Vui lòng nhập mật khẩu.')).toBeInTheDocument()
    expect(screen.getByLabelText('Email công ty')).toHaveAttribute('aria-invalid', 'true')
    expect(session.signInWithPassword).not.toHaveBeenCalled()
  })

  it('refuses an email that is not one', async () => {
    renderForm()
    await signIn('nhanvien')

    expect(await screen.findByText('Email không hợp lệ.')).toBeInTheDocument()
    expect(session.signInWithPassword).not.toHaveBeenCalled()
  })

  it('shows and hides the password with one toggle button', async () => {
    renderForm()
    const user = userEvent.setup()
    const password = screen.getByLabelText('Mật khẩu')
    const toggle = screen.getByRole('button', { name: 'Hiện mật khẩu' })
    expect(password).toHaveAttribute('type', 'password')
    expect(toggle).toHaveAttribute('aria-pressed', 'false')

    await user.click(toggle)
    expect(password).toHaveAttribute('type', 'text')
    // A toggle keeps its label; its state is aria-pressed (WAI-ARIA APG, Button).
    expect(toggle).toHaveAccessibleName('Hiện mật khẩu')
    expect(toggle).toHaveAttribute('aria-pressed', 'true')
  })

  it('signs in with the trimmed email and the password as typed', async () => {
    renderForm()
    await signIn('  nhanvien@zengear.test ', ' mat khau ')

    await waitFor(() =>
      expect(session.signInWithPassword).toHaveBeenCalledWith(
        'nhanvien@zengear.test',
        ' mat khau ',
      ),
    )
  })
})

describe('answers of Firebase', () => {
  it.each([
    ['auth/invalid-credential', 'Email hoặc mật khẩu không đúng.'],
    ['auth/wrong-password', 'Email hoặc mật khẩu không đúng.'],
    ['auth/user-not-found', 'Email hoặc mật khẩu không đúng.'],
    ['auth/too-many-requests', 'Bạn đã thử quá nhiều lần. Vui lòng thử lại sau.'],
    ['auth/user-disabled', 'Tài khoản đã bị khóa. Vui lòng liên hệ quản lý cửa hàng.'],
    [
      'auth/network-request-failed',
      'Không kết nối được máy chủ. Vui lòng kiểm tra mạng và thử lại.',
    ],
    ['auth/internal-error', 'Không đăng nhập được. Vui lòng thử lại.'],
  ])('%s shows its message', async (code, text) => {
    session.signInWithPassword.mockRejectedValue(firebaseError(code))
    renderForm()
    await signIn()

    expect(await screen.findByRole('alert')).toHaveTextContent(text)
    expect(api.me).not.toHaveBeenCalled()
    expect(navigation.replace).not.toHaveBeenCalled()
  })
})

describe('answers of GET /api/v1/admin/me', () => {
  it.each([
    [
      '403 account-disabled',
      problem(403, 'account-disabled'),
      'Tài khoản đã bị khóa. Vui lòng liên hệ quản lý cửa hàng.',
    ],
    [
      '403 account-not-registered',
      problem(403, 'account-not-registered'),
      'Tài khoản này chưa được đăng ký trong hệ thống.',
    ],
    ['401', new ApiError(401), 'Không đăng nhập được. Vui lòng thử lại.'],
    ['500', new ApiError(500), 'Không đăng nhập được. Vui lòng thử lại.'],
    [
      'a network error',
      new TypeError('Failed to fetch'),
      'Không kết nối được máy chủ. Vui lòng kiểm tra mạng và thử lại.',
    ],
  ])('%s shows its message and signs out', async (_, error, text) => {
    api.me.mockRejectedValue(error)
    renderForm()
    await signIn()

    expect(await screen.findByRole('alert')).toHaveTextContent(text)
    expect(session.signOutAndClear).toHaveBeenCalledOnce()
    expect(navigation.replace).not.toHaveBeenCalled()
  })

  it('signs out an account that is not a staff account', async () => {
    api.me.mockResolvedValue({ ...staff, isStaff: false, permissions: [] })
    renderForm()
    await signIn()

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Tài khoản này không phải tài khoản nhân viên.',
    )
    expect(session.signOutAndClear).toHaveBeenCalledOnce()
  })

  it('sends a staff member who must change the password to SCR-AUT-02', async () => {
    api.me.mockResolvedValue({ ...staff, mustChangePassword: true })
    renderForm('next=/admin/staff')
    await signIn()

    await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith('/admin/change-password'))
  })
})

describe('where a staff member goes', () => {
  it('goes to the next path', async () => {
    renderForm('next=%2Fadmin%2Fsettings%2Fbusiness-location%3Ftab%3D1')
    await signIn()

    await waitFor(() =>
      expect(navigation.replace).toHaveBeenCalledWith('/admin/settings/business-location?tab=1'),
    )
  })

  it('ignores a next path outside the admin part', async () => {
    renderForm('next=https%3A%2F%2Fevil.example%2Fadmin')
    await signIn()

    await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith('/admin/staff'))
  })

  it('goes to the staff list with staff.read', async () => {
    renderForm()
    await signIn()

    await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith('/admin/staff'))
  })

  it('goes to the account without staff.read', async () => {
    api.me.mockResolvedValue({ ...staff, permissions: [] })
    renderForm()
    await signIn()

    await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith('/admin/account'))
  })

  it('goes on at once when already signed in', async () => {
    session.hasSession.mockResolvedValue(true)
    renderForm()

    await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith('/admin/staff'))
    expect(session.signInWithPassword).not.toHaveBeenCalled()
  })

  it('sends one sign-in for a double click', async () => {
    let finish: () => void = () => {}
    session.signInWithPassword.mockReturnValue(new Promise<void>((resolve) => (finish = resolve)))
    renderForm()
    const user = await signIn()
    await user.click(screen.getByRole('button', { name: 'Đăng nhập' }))
    finish()

    await waitFor(() => expect(navigation.replace).toHaveBeenCalledOnce())
    expect(session.signInWithPassword).toHaveBeenCalledOnce()
    const button = screen.getByRole('button', { name: 'Đăng nhập' })
    expect(button).toBeDisabled()
    expect(button).toHaveAttribute('aria-busy', 'true')
  })

  it('stays usable when Firebase cannot read its session', async () => {
    session.hasSession.mockRejectedValue(new Error('storage blocked'))
    renderForm()

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Không đăng nhập được. Vui lòng thử lại.',
    )
    const button = screen.getByRole('button', { name: 'Đăng nhập' })
    expect(button).toBeEnabled()
    expect(button).toHaveAttribute('aria-busy', 'false')
  })

  it('shows a message when the sign-out after a refusal fails', async () => {
    api.me.mockResolvedValue({ ...staff, isStaff: false })
    session.signOutAndClear.mockRejectedValue(new Error('sign-out failed'))
    renderForm()
    await signIn()

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Không đăng nhập được. Vui lòng thử lại.',
    )
  })
})

describe('the reason of a sign-out', () => {
  it.each([
    ['account-disabled', 'Tài khoản đã bị khóa. Vui lòng liên hệ quản lý cửa hàng.'],
    ['account-not-registered', 'Tài khoản này chưa được đăng ký trong hệ thống.'],
    ['not-staff', 'Tài khoản này không phải tài khoản nhân viên.'],
  ])('%s shows its message', async (reason, text) => {
    renderForm(`reason=${reason}`)

    expect(screen.getByRole('alert')).toHaveTextContent(text)
  })

  it('ignores an unknown reason', () => {
    renderForm('reason=%3Cscript%3E')

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })
})
