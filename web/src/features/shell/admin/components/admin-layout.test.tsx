import { act, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { CurrentUser } from '@/features/identity/access'
import { renderWithProviders } from '@/test/render'
import { AdminHeaderSection } from './admin-header-section'
import { AdminLayout } from './admin-layout'
import { AdminNotFound } from './admin-not-found'
import { AdminPageSection } from './admin-page-section'
import { AdminShell } from './admin-shell'
import { AdminSidebarSection } from './admin-sidebar-section'
import { RequirePermission } from './no-permission'
import { ThemeProvider } from '@/features/shell/theme'

// The frame of the Layout of SCR-ADM-01 (docs/SCREENS.md): the menu that collapses to icons, its narrow sheet, the
// header and the pages of the frame itself. The guard and the answers of the API are in admin-shell.test.tsx.

const navigation = vi.hoisted(() => ({ replace: vi.fn(), pathname: '/admin/staff' }))
vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace: navigation.replace }),
  usePathname: () => navigation.pathname,
}))

vi.mock('@/lib/auth/session', () => ({
  useAuthState: () => 'signed-in',
  signOutAndClear: vi.fn(),
}))

const api = vi.hoisted(() => ({ me: vi.fn() }))
vi.mock('@/features/identity/access/api/me', () => ({
  meQuery: { queryKey: ['me'], queryFn: () => api.me(), meta: { reportErrors: false } },
}))

const admin: CurrentUser = {
  id: '0199a0f3-0000-7000-8000-000000000001',
  name: 'Chủ cửa hàng',
  email: 'owner@zengear.test',
  isStaff: true,
  mustChangePassword: false,
  permissions: ['business_location.update', 'staff.read'],
}

const renderFrame = (page: React.ReactNode = <p>Nội dung trang</p>, defaultOpen = true) =>
  renderWithProviders(
    // As app/(admin)/admin/layout.tsx wraps the frame.
    <ThemeProvider>
      <AdminShell defaultOpen={defaultOpen}>
        <AdminLayout
          defaultOpen={defaultOpen}
          sidebar={<AdminSidebarSection />}
          header={<AdminHeaderSection />}
        >
          <AdminPageSection>{page}</AdminPageSection>
        </AdminLayout>
      </AdminShell>
    </ThemeProvider>,
  )

const menu = () => screen.getByRole('navigation', { name: 'Menu' })
const findMenu = () => screen.findByRole('navigation', { name: 'Menu' })
const cookie = () => /sidebar_state=(\w+)/.exec(document.cookie)?.[1]
const wideScreen = window.innerWidth

beforeEach(() => {
  vi.resetAllMocks()
  navigation.pathname = '/admin/staff'
  api.me.mockResolvedValue(admin)
  document.cookie = 'sidebar_state=; max-age=0; path=/'
  localStorage.clear()
  document.documentElement.removeAttribute('class')
})

afterEach(() => {
  window.innerWidth = wideScreen
})

describe('on a wide screen', () => {
  it('starts with a skip link to the page, which sits in its own main', async () => {
    renderFrame()
    await screen.findByText('Nội dung trang')

    expect(screen.getByRole('link', { name: 'Bỏ qua đến nội dung chính' })).toHaveAttribute(
      'href',
      '#main',
    )
    expect(screen.getByRole('main')).toHaveAttribute('id', 'main')
    expect(within(screen.getByRole('main')).getByText('Nội dung trang')).toBeInTheDocument()
    expect(screen.getByRole('banner')).toBeInTheDocument()
  })

  it('collapses the menu and keeps its state in the cookie, by the button or Ctrl+B', async () => {
    renderFrame()
    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'Thu gọn menu' }))

    expect(screen.getByRole('button', { name: 'Mở rộng menu' })).toBeInTheDocument()
    expect(cookie()).toBe('false')

    await user.keyboard('{Control>}b{/Control}')
    expect(screen.getByRole('button', { name: 'Thu gọn menu' })).toBeInTheDocument()
    expect(cookie()).toBe('true')
  })

  it('starts collapsed when the cookie says so', async () => {
    renderFrame(undefined, false)

    expect(await screen.findByRole('button', { name: 'Mở rộng menu' })).toBeInTheDocument()
  })

  it('opens the menu with the group open when "Cài đặt" is clicked collapsed', async () => {
    renderFrame(undefined, false)
    const user = userEvent.setup()
    await user.click(within(await findMenu()).getByRole('button', { name: 'Cài đặt' }))

    expect(screen.getByRole('button', { name: 'Thu gọn menu' })).toBeInTheDocument()
    expect(within(menu()).getByRole('link', { name: 'Địa điểm kinh doanh' })).toBeInTheDocument()
  })

  it('opens the group of the current page, and the breadcrumb says where it sits', async () => {
    navigation.pathname = '/admin/settings/business-location'
    renderFrame()

    const entry = within(await findMenu()).getByRole('link', { name: 'Địa điểm kinh doanh' })
    expect(entry).toHaveAttribute('aria-current', 'page')
    expect(within(menu()).getByRole('button', { name: 'Cài đặt' })).toHaveAttribute(
      'aria-expanded',
      'true',
    )
    const breadcrumb = screen.getByRole('navigation', { name: 'Vị trí trang' })
    expect(breadcrumb).toHaveTextContent('Cài đặtĐịa điểm kinh doanh')
    expect(within(breadcrumb).getByText('Địa điểm kinh doanh')).toHaveAttribute(
      'aria-current',
      'page',
    )
  })

  it('shows the user with the email in the user menu', async () => {
    renderFrame()
    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'Chủ cửa hàng' }))

    const content = await screen.findByRole('menu')
    expect(within(content).getByText('owner@zengear.test')).toBeInTheDocument()
    expect(within(content).getByRole('menuitem', { name: 'Đăng xuất' })).toBeInTheDocument()
  })

  it('has no group "Menu" for a user whose permissions allow no entry', async () => {
    api.me.mockResolvedValue({ ...admin, permissions: [] })
    renderFrame()

    expect(await screen.findByRole('button', { name: 'Chủ cửa hàng' })).toBeInTheDocument()
    expect(screen.queryByRole('navigation', { name: 'Menu' })).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'ZenGear' })).toHaveAttribute('href', '/admin')
  })
})

describe('on a narrow screen', () => {
  it('opens the menu in a sheet, which "Đóng menu" and a menu link close', async () => {
    window.innerWidth = 390
    renderFrame()
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'Mở menu' }))
    const sheet = await screen.findByRole('dialog', { name: 'Menu' })
    await user.click(within(sheet).getByRole('button', { name: 'Đóng menu' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())

    await user.click(screen.getByRole('button', { name: 'Mở menu' }))
    await user.click(
      within(await screen.findByRole('dialog', { name: 'Menu' })).getByRole('link', {
        name: 'Nhân viên',
      }),
    )
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it.each([
    ['the store name', ['link', 'ZenGear']],
    ['"Tài khoản của tôi" of the user menu', ['menuitem', 'Tài khoản của tôi']],
  ] as const)('closes the sheet when %s opens a page', async (_, [role, name]) => {
    window.innerWidth = 390
    renderFrame()
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'Mở menu' }))
    const sheet = await screen.findByRole('dialog', { name: 'Menu' })
    if (role === 'menuitem') {
      await user.click(within(sheet).getByRole('button', { name: 'Chủ cửa hàng' }))
    }
    await user.click(await screen.findByRole(role, { name }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })
})

// DEC-123: "Giao diện" in the header from md up, in the user menu below; the system theme by default (the matchMedia
// of src/test/setup.ts answers light), the choice kept in localStorage.
describe('the theme', () => {
  const html = document.documentElement
  const radio = (name: string) => screen.getByRole('menuitemradio', { name })

  it('switches from the header on a wide screen, and the user menu has no "Giao diện"', async () => {
    renderFrame()
    const user = userEvent.setup()
    const button = await screen.findByRole('button', { name: 'Giao diện' })
    await user.hover(button)
    expect(await screen.findByRole('tooltip', {}, { timeout: 2000 })).toHaveTextContent('Giao diện')

    await user.click(button)
    const options = await screen.findByRole('menu', { name: 'Giao diện' })
    expect(
      within(options)
        .getAllByRole('menuitemradio')
        .map((item) => item.textContent),
    ).toEqual(['Sáng', 'Tối', 'Theo hệ thống'])
    expect(radio('Theo hệ thống')).toHaveAttribute('aria-checked', 'true')
    expect(html).toHaveClass('light')

    await user.click(radio('Tối'))
    await waitFor(() => expect(html).toHaveClass('dark'))
    expect(localStorage.getItem('zengear-theme')).toBe('dark')
    await user.click(button)
    expect(radio('Tối')).toHaveAttribute('aria-checked', 'true')
    await user.keyboard('{Escape}')

    await user.click(screen.getByRole('button', { name: 'Chủ cửa hàng' }))
    await screen.findByRole('menuitem', { name: 'Đăng xuất' })
    expect(screen.queryByRole('menuitem', { name: 'Giao diện' })).not.toBeInTheDocument()
  })

  it('starts with the choice kept in the browser', async () => {
    localStorage.setItem('zengear-theme', 'dark')
    renderFrame()
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'Giao diện' }))
    expect(radio('Tối')).toHaveAttribute('aria-checked', 'true')
    expect(html).toHaveClass('dark')
  })

  it('switches from the user menu on a narrow screen, where the header has no button', async () => {
    window.innerWidth = 390
    localStorage.setItem('zengear-theme', 'dark')
    renderFrame()
    const user = userEvent.setup()

    const openMenu = await screen.findByRole('button', { name: 'Mở menu' })
    // Before the sheet opens: then Radix hides the rest of the page from the accessibility tree.
    expect(screen.queryByRole('button', { name: 'Giao diện' })).not.toBeInTheDocument()
    await user.click(openMenu)
    const sheet = await screen.findByRole('dialog', { name: 'Menu' })
    await user.click(within(sheet).getByRole('button', { name: 'Chủ cửa hàng' }))
    await user.click(await screen.findByRole('menuitem', { name: 'Giao diện' }))
    expect(await screen.findByRole('menuitemradio', { name: 'Tối' })).toHaveAttribute(
      'aria-checked',
      'true',
    )

    // By the keyboard: jsdom has no layout, so Radix closes a submenu as soon as the pointer leaves its trigger.
    act(() => radio('Sáng').focus())
    await user.keyboard('{Enter}')
    await waitFor(() => expect(html).toHaveClass('light'))
    expect(html).not.toHaveClass('dark')
    expect(localStorage.getItem('zengear-theme')).toBe('light')
  })
})

describe('the pages of the frame', () => {
  it('the 404 says what to do and where help is', async () => {
    navigation.pathname = '/admin/khong-ton-tai'
    renderFrame(<AdminNotFound />)

    const page = await screen.findByRole('main')
    expect(
      await within(page).findByRole('heading', { name: 'Không tìm thấy trang này.' }),
    ).toBeInTheDocument()
    expect(within(page).getByText(/Đường dẫn có thể đã sai/)).toBeInTheDocument()
    expect(within(page).getByRole('link', { name: 'Về trang chính' })).toHaveAttribute(
      'href',
      '/admin',
    )
    expect(within(page).getByText('Cần trợ giúp? Liên hệ quản trị viên.')).toBeInTheDocument()
    expect(screen.queryByRole('navigation', { name: 'Vị trí trang' })).not.toBeInTheDocument()
  })

  it('the no-permission page gives its reason and keeps the breadcrumb of the path', async () => {
    api.me.mockResolvedValue({ ...admin, permissions: [] })
    renderFrame(
      <RequirePermission permission="staff.read">
        <p>Danh sách</p>
      </RequirePermission>,
    )

    const page = await screen.findByRole('main')
    expect(
      await within(page).findByRole('heading', { name: 'Bạn không có quyền xem trang này.' }),
    ).toBeInTheDocument()
    expect(
      within(page).getByText('Vai trò của bạn chưa được cấp quyền xem trang này.'),
    ).toBeInTheDocument()
    expect(within(page).getByText('Cần trợ giúp? Liên hệ quản trị viên.')).toBeInTheDocument()
    expect(screen.getByRole('navigation', { name: 'Vị trí trang' })).toHaveTextContent('Nhân viên')
  })
})
