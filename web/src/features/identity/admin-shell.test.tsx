import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { NextIntlClientProvider } from 'next-intl'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { reportApiError } from '@/lib/api/api-errors'
import { ApiError } from '@/lib/api/problems'
import type { AuthState } from '@/lib/auth/session'
import messages from '../../../messages/vi.json'
import { AdminShell } from './admin-shell'
import { ChangePasswordScreen } from './change-password-screen'
import type { CurrentUser } from './me'
import { RequirePermission } from './no-permission'

const navigation = vi.hoisted(() => ({ replace: vi.fn(), pathname: '/admin/staff' }))
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

const api = vi.hoisted(() => ({ me: vi.fn() }))
vi.mock('./me', () => ({ meQuery: { queryKey: ['me'], queryFn: () => api.me() } }))

const admin: CurrentUser = {
  id: '0199a0f3-0000-7000-8000-000000000001',
  name: 'Chủ cửa hàng',
  email: 'owner@zengear.test',
  isStaff: true,
  mustChangePassword: false,
  permissions: ['business_location.update', 'staff.read'],
}
const manager: CurrentUser = { ...admin, name: 'Quản lý (fixture)', permissions: ['staff.read'] }
const consultant: CurrentUser = { ...admin, name: 'Tư vấn bán hàng', permissions: [] }

const problem = (status: number, name: string) =>
  new ApiError(status, { type: `/problems/${name}`, status })

function renderWithProviders(ui: React.ReactNode) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const wrap = (node: React.ReactNode) => (
    <NextIntlClientProvider locale="vi" messages={messages} timeZone="Asia/Ho_Chi_Minh">
      <QueryClientProvider client={client}>{node}</QueryClientProvider>
    </NextIntlClientProvider>
  )
  const result = render(wrap(ui))
  return { client, rerender: (node: React.ReactNode) => result.rerender(wrap(node)) }
}

const renderShell = (page: React.ReactNode = <p>Nội dung trang</p>) =>
  renderWithProviders(<AdminShell>{page}</AdminShell>)

beforeEach(() => {
  vi.resetAllMocks()
  session.state = 'signed-in'
  session.signOutAndClear.mockResolvedValue(undefined)
  navigation.pathname = '/admin/staff'
  window.history.replaceState(null, '', '/admin/staff?page=2')
  api.me.mockResolvedValue(admin)
})

describe('the guard', () => {
  it('sends a visitor without a session to the sign-in page, keeping the path', async () => {
    session.state = 'signed-out'
    renderShell()

    await waitFor(() =>
      expect(navigation.replace).toHaveBeenCalledWith(
        '/admin/sign-in?next=%2Fadmin%2Fstaff%3Fpage%3D2',
      ),
    )
    expect(api.me).not.toHaveBeenCalled()
    expect(screen.queryByText('Nội dung trang')).not.toBeInTheDocument()
  })

  it('shows the layout skeleton while the session loads, never the page', () => {
    session.state = 'loading'
    renderShell()

    expect(screen.queryByRole('navigation')).not.toBeInTheDocument()
    expect(screen.queryByText('Nội dung trang')).not.toBeInTheDocument()
  })

  it('sends a staff member who must change the password to SCR-AUT-02', async () => {
    api.me.mockResolvedValue({ ...admin, mustChangePassword: true })
    renderShell()

    await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith('/admin/change-password'))
    expect(screen.queryByText('Nội dung trang')).not.toBeInTheDocument()
  })

  it.each([
    ['an account that is not a staff account', () => ({ ...admin, isStaff: false }), 'not-staff'],
  ])('signs out %s with its reason', async (_, me, reason) => {
    api.me.mockResolvedValue(me())
    renderShell()

    await waitFor(() =>
      expect(navigation.replace).toHaveBeenCalledWith(`/admin/sign-in?reason=${reason}`),
    )
    expect(session.signOutAndClear).toHaveBeenCalledOnce()
  })

  it.each([
    ['403 account-disabled', problem(403, 'account-disabled'), '?reason=account-disabled'],
    [
      '403 account-not-registered',
      problem(403, 'account-not-registered'),
      '?reason=account-not-registered',
    ],
    ['401', new ApiError(401), '?next=%2Fadmin%2Fstaff%3Fpage%3D2'],
  ])('signs out on %s of GET /me', async (_, error, query) => {
    api.me.mockRejectedValue(error)
    renderShell()

    await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith(`/admin/sign-in${query}`))
    expect(session.signOutAndClear).toHaveBeenCalledOnce()
  })

  it.each([
    ['403 account-disabled', problem(403, 'account-disabled'), '?reason=account-disabled'],
    ['401', new ApiError(401), '?next=%2Fadmin%2Fstaff%3Fpage%3D2'],
  ])('signs out when a refetch of GET /me answers %s', async (_, error, query) => {
    const { client } = renderShell()
    await screen.findByText('Nội dung trang')

    api.me.mockRejectedValue(error)
    await act(() => client.refetchQueries({ queryKey: ['me'] }))

    await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith(`/admin/sign-in${query}`))
    expect(session.signOutAndClear).toHaveBeenCalledOnce()
  })

  it('keeps the page when a refetch of GET /me fails otherwise', async () => {
    const { client } = renderShell()
    await screen.findByText('Nội dung trang')

    api.me.mockRejectedValue(new ApiError(503))
    await act(() => client.refetchQueries({ queryKey: ['me'] }))

    expect(screen.getByText('Nội dung trang')).toBeInTheDocument()
    expect(navigation.replace).not.toHaveBeenCalled()
  })

  it('offers to try again or sign out when GET /me fails otherwise', async () => {
    api.me.mockRejectedValueOnce(new ApiError(503)).mockResolvedValue(admin)
    renderShell()

    expect(await screen.findByRole('alert')).toHaveTextContent('Đã có lỗi xảy ra.')
    expect(screen.getByRole('button', { name: 'Đăng xuất' })).toBeInTheDocument()
    await userEvent.setup().click(screen.getByRole('button', { name: 'Thử lại' }))

    expect(await screen.findByText('Nội dung trang')).toBeInTheDocument()
    expect(session.signOutAndClear).not.toHaveBeenCalled()
  })
})

describe('the layout', () => {
  it('shows the name, the page and every menu entry the permissions allow', async () => {
    renderShell()

    expect(await screen.findByText('Nội dung trang')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Chủ cửa hàng' })).toBeInTheDocument()
    const menu = screen.getByRole('navigation', { name: 'Menu' })
    expect(within(menu).getByRole('link', { name: 'Nhân viên' })).toHaveAttribute(
      'aria-current',
      'page',
    )
    expect(within(menu).getByText('Cài đặt')).toBeInTheDocument()
    expect(within(menu).getByRole('link', { name: 'Địa điểm kinh doanh' })).toHaveAttribute(
      'href',
      '/admin/settings/business-location',
    )
  })

  it('hides the entries without their permission', async () => {
    api.me.mockResolvedValue(manager)
    renderShell()

    const menu = await screen.findByRole('navigation', { name: 'Menu' })
    expect(within(menu).getByRole('link', { name: 'Nhân viên' })).toBeInTheDocument()
    expect(within(menu).queryByText('Cài đặt')).not.toBeInTheDocument()
    expect(
      within(menu).queryByRole('link', { name: 'Địa điểm kinh doanh' }),
    ).not.toBeInTheDocument()
  })

  it('offers the error state when the sign-out fails', async () => {
    session.signOutAndClear.mockRejectedValue(new Error('network'))
    renderShell()
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'Chủ cửa hàng' }))
    await user.click(await screen.findByRole('menuitem', { name: 'Đăng xuất' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Đã có lỗi xảy ra.')
    expect(navigation.replace).not.toHaveBeenCalled()
  })

  it('signs out from the user menu to the sign-in page, without a next path', async () => {
    renderShell()
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'Chủ cửa hàng' }))
    expect(await screen.findByRole('menuitem', { name: 'Tài khoản của tôi' })).toHaveAttribute(
      'href',
      '/admin/account',
    )
    await user.click(screen.getByRole('menuitem', { name: 'Đăng xuất' }))

    await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith('/admin/sign-in'))
    expect(session.signOutAndClear).toHaveBeenCalledOnce()
  })
})

describe('the answers of other calls', () => {
  it('shows the no-permission page for 403 permission-denied until the path changes', async () => {
    const { rerender } = renderShell()
    await screen.findByText('Nội dung trang')
    const page = (
      <AdminShell>
        <p>Nội dung trang</p>
      </AdminShell>
    )

    act(() => reportApiError(problem(403, 'permission-denied')))

    expect(await screen.findByText('Bạn không có quyền xem trang này.')).toBeInTheDocument()
    expect(screen.queryByText('Nội dung trang')).not.toBeInTheDocument()
    // The link leads away from this page: to the next entry the user can open.
    expect(
      within(screen.getByRole('main')).getByRole('link', { name: 'Địa điểm kinh doanh' }),
    ).toHaveAttribute('href', '/admin/settings/business-location')

    navigation.pathname = '/admin/account'
    rerender(page)
    expect(await screen.findByText('Nội dung trang')).toBeInTheDocument()

    // Back on the page, nothing is remembered: only a new 403 shows the no-permission page again.
    navigation.pathname = '/admin/staff'
    rerender(page)
    expect(screen.getByText('Nội dung trang')).toBeInTheDocument()
  })

  it('clears the no-permission page when its link is followed', async () => {
    api.me.mockResolvedValue(manager)
    renderShell()
    await screen.findByText('Nội dung trang')

    act(() => reportApiError(problem(403, 'permission-denied')))
    const link = await within(screen.getByRole('main')).findByRole('link', {
      name: 'Tài khoản của tôi',
    })
    await userEvent.setup().click(link)

    expect(await screen.findByText('Nội dung trang')).toBeInTheDocument()
  })

  it('signs out when another call says the account was disabled', async () => {
    renderShell()
    await screen.findByText('Nội dung trang')

    act(() => reportApiError(problem(403, 'account-disabled')))

    await waitFor(() =>
      expect(navigation.replace).toHaveBeenCalledWith('/admin/sign-in?reason=account-disabled'),
    )
  })

  it('leaves the other problems to the screen', async () => {
    renderShell()
    await screen.findByText('Nội dung trang')

    act(() => reportApiError(problem(409, 'last-active-admin')))

    expect(screen.getByText('Nội dung trang')).toBeInTheDocument()
    expect(navigation.replace).not.toHaveBeenCalled()
  })
})

describe('RequirePermission', () => {
  it('shows the page with the permission', async () => {
    renderShell(
      <RequirePermission permission="staff.read">
        <p>Danh sách</p>
      </RequirePermission>,
    )
    expect(await screen.findByText('Danh sách')).toBeInTheDocument()
  })

  it('shows the no-permission page with a link to the account when no entry is allowed', async () => {
    api.me.mockResolvedValue(consultant)
    renderShell(
      <RequirePermission permission="staff.read">
        <p>Danh sách</p>
      </RequirePermission>,
    )

    expect(await screen.findByText('Bạn không có quyền xem trang này.')).toBeInTheDocument()
    expect(screen.queryByText('Danh sách')).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Tài khoản của tôi' })).toHaveAttribute(
      'href',
      '/admin/account',
    )
  })
})

describe('ChangePasswordScreen', () => {
  it('stays while the password change is pending, with "Đăng xuất"', async () => {
    api.me.mockResolvedValue({ ...consultant, mustChangePassword: true })
    renderWithProviders(<ChangePasswordScreen />)

    expect(
      await screen.findByText(
        'Màn hình đổi mật khẩu sẽ có ở bản sau. Vui lòng liên hệ quản lý cửa hàng.',
      ),
    ).toBeInTheDocument()
    await userEvent.setup().click(screen.getByRole('button', { name: 'Đăng xuất' }))
    await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith('/admin/sign-in'))
  })

  it('sends a staff member without a pending change to the landing page', async () => {
    api.me.mockResolvedValue(manager)
    renderWithProviders(<ChangePasswordScreen />)

    await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith('/admin/staff'))
  })

  it('sends a visitor without a session to the sign-in page', async () => {
    session.state = 'signed-out'
    window.history.replaceState(null, '', '/admin/change-password')
    renderWithProviders(<ChangePasswordScreen />)

    await waitFor(() =>
      expect(navigation.replace).toHaveBeenCalledWith(
        '/admin/sign-in?next=%2Fadmin%2Fchange-password',
      ),
    )
  })
})
