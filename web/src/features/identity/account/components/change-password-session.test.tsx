import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { CurrentUser } from '@/features/identity/access'
import { ApiError } from '@/lib/api/problems'
import type { AuthState } from '@/lib/auth/session'
import { renderWithProviders } from '@/test/render'
import { ChangePasswordLayout } from './change-password-layout'
import {
  ChangePasswordActions,
  ChangePasswordContent,
  ChangePasswordSession,
} from './change-password-session'

const navigation = vi.hoisted(() => ({ replace: vi.fn(), pathname: '/admin/change-password' }))
vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace: navigation.replace }),
  usePathname: () => navigation.pathname,
}))

const session = vi.hoisted(() => ({
  state: 'signed-in' as AuthState,
  signOutAndClear: vi.fn(),
}))
vi.mock('@/lib/auth/session', () => ({
  useAuthState: () => session.state,
  signOutAndClear: session.signOutAndClear,
}))

const api = vi.hoisted(() => ({ me: vi.fn(), changeMyPassword: vi.fn() }))
// The files the session and the form import: GET /me of the session, and the call of this module.
vi.mock('@/features/identity/access/api/me', () => ({
  meQuery: { queryKey: ['me'], queryFn: () => api.me(), meta: { reportErrors: false } },
}))
vi.mock('../api/me', () => ({
  changeMyPassword: api.changeMyPassword,
  changePasswordKey: ['postApiV1AdminMeChangePassword'],
}))

const consultant: CurrentUser = {
  id: '0199a0f3-0000-7000-8000-000000000009',
  name: 'Tư vấn bán hàng',
  email: 'tuvan@zengear.test',
  isStaff: true,
  mustChangePassword: true,
  permissions: [],
}
const manager: CurrentUser = {
  ...consultant,
  name: 'Quản lý (fixture)',
  mustChangePassword: false,
  permissions: ['staff.read'],
}

// SCR-AUT-02 as its page composes it.
const renderScreen = () =>
  renderWithProviders(
    <ChangePasswordSession>
      <ChangePasswordLayout title="Đổi mật khẩu" actions={<ChangePasswordActions />}>
        <ChangePasswordContent />
      </ChangePasswordLayout>
    </ChangePasswordSession>,
  )

beforeEach(() => {
  vi.resetAllMocks()
  window.sessionStorage.clear()
  session.state = 'signed-in'
  session.signOutAndClear.mockResolvedValue(undefined)
  window.history.replaceState(null, '', '/admin/change-password')
  api.me.mockResolvedValue(consultant)
})

describe('ChangePasswordScreen', () => {
  it('shows the form while the password change is pending, with "Đăng xuất"', async () => {
    renderScreen()

    expect(await screen.findByLabelText('Mật khẩu mới')).toBeInTheDocument()
    expect(screen.getByLabelText('Nhập lại mật khẩu mới')).toBeInTheDocument()
    await userEvent.setup().click(screen.getByRole('button', { name: 'Đăng xuất' }))
    await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith('/admin/sign-in'))
  })

  it('signs out after the change, with the notice of the sign-in page', async () => {
    api.changeMyPassword.mockResolvedValue(undefined)
    renderScreen()
    const user = userEvent.setup()

    await user.type(await screen.findByLabelText('Mật khẩu mới'), 'mot mat khau moi dai')
    await user.type(screen.getByLabelText('Nhập lại mật khẩu mới'), 'mot mat khau moi dai')
    await user.click(screen.getByRole('button', { name: 'Đổi mật khẩu' }))

    await waitFor(() =>
      expect(navigation.replace).toHaveBeenCalledWith('/admin/sign-in?reason=password-changed'),
    )
    expect(session.signOutAndClear).toHaveBeenCalledOnce()
    expect(api.changeMyPassword).toHaveBeenCalledExactlyOnceWith('mot mat khau moi dai')
  })

  it("shows the API's text when GET /me fails with one", async () => {
    api.me.mockRejectedValue(
      new ApiError(403, {
        type: '/problems/forbidden',
        status: 403,
        detail: 'Bạn không được phép thực hiện thao tác này.',
      }),
    )
    renderScreen()

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Bạn không được phép thực hiện thao tác này.',
    )
    expect(screen.getByRole('button', { name: 'Thử lại' })).toBeInTheDocument()
  })

  it('sends a staff member without a pending change to the landing page', async () => {
    api.me.mockResolvedValue(manager)
    renderScreen()

    await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith('/admin/staff'))
  })

  it('sends a visitor without a session to the sign-in page', async () => {
    session.state = 'signed-out'
    renderScreen()

    await waitFor(() =>
      expect(navigation.replace).toHaveBeenCalledWith(
        '/admin/sign-in?next=%2Fadmin%2Fchange-password',
      ),
    )
  })
})
