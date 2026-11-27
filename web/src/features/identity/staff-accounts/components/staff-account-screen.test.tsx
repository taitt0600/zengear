import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ADMIN_ID, calls, manager, ok, refused, renderAccount } from './staff-account.fixtures'

// SCR-STF-03 Staff account (docs/screens/SCR-STF-03.md): what the page shows of an account, by its status and the
// user's rights, in the cards "Hồ sơ nhân viên" and "Tài khoản" with the section navigation.

const api = vi.hoisted(() => ({ fetch: vi.fn() }))
vi.mock('@/lib/api/api', () => ({ apiFetch: api.fetch }))
vi.mock('@/lib/auth/session', () => ({}))
const toast = vi.hoisted(() => ({ error: vi.fn(), success: vi.fn() }))
vi.mock('sonner', () => ({ toast }))
// The notice of a PENDING account holds the router, for after a rejection.
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: vi.fn(), replace: vi.fn() }) }))

const details = () => screen.getByRole('region', { name: 'Hồ sơ nhân viên' })
const account = () => screen.getByRole('region', { name: 'Tài khoản' })
/** The value of the row `label` of a card: the definition its term names. */
const value = (card: HTMLElement, label: string) =>
  within(card).getByRole('definition', { name: label })

beforeEach(() => {
  vi.resetAllMocks()
  api.fetch.mockResolvedValue(ok(manager))
})

describe('the staff account page', () => {
  it('shows the person, their staff profile and their sign-in account in two cards', async () => {
    renderAccount()

    expect(await screen.findByRole('heading', { level: 1, name: 'Lê Thu Hà' })).toBeVisible()
    expect(screen.getByText('haltzg260002@zengear.test · ZG260002')).toBeVisible()
    expect(
      within(details()).getByText(
        'Thông tin của nhân viên tại cửa hàng, dùng để liên hệ trong công việc.',
      ),
    ).toBeVisible()
    expect(value(details(), 'Họ tên')).toHaveTextContent('Lê Thu Hà')
    expect(value(details(), 'Mã nhân viên')).toHaveTextContent('ZG260002')
    expect(value(details(), 'SĐT công việc')).toHaveTextContent('—')
    expect(value(details(), 'Ngày vào làm')).toHaveTextContent('01/10/2026')
    expect(value(details(), 'Ngày nghỉ việc')).toHaveTextContent('—')
    expect(within(details()).getByText('LH')).toBeInTheDocument()
    expect(
      within(account()).getByText(
        'Email đăng nhập, vai trò, trạng thái và mật khẩu của tài khoản.',
      ),
    ).toBeVisible()
    expect(value(account(), 'Email đăng nhập')).toHaveTextContent('haltzg260002@zengear.test')
    expect(value(account(), 'Vai trò')).toHaveTextContent('Store Manager')
    expect(value(account(), 'Mật khẩu mặc định')).toHaveTextContent('Đã đổi')
    expect(within(value(account(), 'Mật khẩu mặc định')).getByText('Đã đổi')).toHaveAttribute(
      'data-variant',
      'secondary',
    )
    expect(calls(api.fetch)).toEqual([`GET /api/v1/admin/staff/${manager.id}`])
  })

  it('puts the lock in the row of the status, described by its hint', async () => {
    renderAccount()

    const status = value(await screen.findByRole('region', { name: 'Tài khoản' }), 'Trạng thái')
    expect(within(status).getByText('Đang hoạt động')).toBeVisible()
    expect(
      within(account()).getByRole('button', { name: 'Khóa tài khoản' }),
    ).toHaveAccessibleDescription(
      'Tài khoản bị khóa không đăng nhập được; khôi phục để nhân viên dùng lại.',
    )
  })

  it('says when the default password is unchanged', async () => {
    api.fetch.mockResolvedValue(ok({ ...manager, mustChangePassword: true }))
    renderAccount()

    const password = value(
      await screen.findByRole('region', { name: 'Tài khoản' }),
      'Mật khẩu mặc định',
    )
    expect(within(password).getByText('Chưa đổi')).toHaveAttribute('data-variant', 'outline')
  })

  it('makes each card a section the navigation reaches, its title focusable', async () => {
    renderAccount()

    await screen.findByRole('heading', { level: 1, name: 'Lê Thu Hà' })
    expect(details()).toHaveAttribute('id', 'info')
    expect(account()).toHaveAttribute('id', 'account')
    for (const [name, id] of [
      ['Hồ sơ nhân viên', 'info-title'],
      ['Tài khoản', 'account-title'],
    ]) {
      const title = screen.getByRole('heading', { level: 2, name })
      expect(title).toHaveAttribute('id', id)
      expect(title).toHaveAttribute('tabindex', '-1')
    }
    const nav = screen.getByRole('navigation', { name: 'Trên trang này' })
    expect(
      within(nav)
        .getAllByRole('link')
        .map((link) => link.textContent),
    ).toEqual(['Hồ sơ nhân viên', 'Tài khoản'])
    expect(within(nav).getByRole('link', { name: 'Hồ sơ nhân viên' })).toHaveAttribute(
      'aria-current',
      'location',
    )
    expect(document.getElementById('staff-account-end')).toHaveAttribute('aria-hidden', 'true')
  })

  it('shows skeletons until the account is loaded, with the navigation', async () => {
    api.fetch.mockReturnValue(new Promise(() => {}))
    renderAccount()

    expect(screen.queryByRole('heading', { level: 1 })).not.toBeInTheDocument()
    expect(screen.getByRole('navigation', { name: 'Trên trang này' })).toBeInTheDocument()
    for (const id of ['info', 'account'])
      expect(document.getElementById(id)).toHaveAttribute('aria-busy', 'true')
    expect(document.querySelectorAll('[data-slot=skeleton]').length).toBeGreaterThan(9)
  })

  describe('a hash of a card in the address', () => {
    const original = Element.prototype.scrollIntoView
    function loadWithHash(hash: string) {
      window.history.replaceState(null, '', hash)
      const scroll = vi.fn()
      Element.prototype.scrollIntoView = scroll
      let resolve: (value: unknown) => void = () => {}
      api.fetch.mockReturnValue(new Promise((done) => (resolve = done)))
      renderAccount()
      return { scroll, load: () => resolve(ok(manager)) }
    }

    afterEach(() => {
      Element.prototype.scrollIntoView = original
      window.history.replaceState(null, '', window.location.pathname)
    })

    it('scrolls to that card once the account has loaded, not while the skeletons show', async () => {
      const { scroll, load } = loadWithHash('#account')
      expect(scroll).not.toHaveBeenCalled()

      load()
      await screen.findByRole('heading', { level: 1, name: 'Lê Thu Hà' })

      await waitFor(() => expect(scroll).toHaveBeenCalledTimes(1))
      expect(scroll).toHaveBeenCalledWith({ block: 'start' })
      expect(scroll.mock.contexts[0]).toBe(document.getElementById('account'))
    })

    it.each(['#account-title', '#nothing'])(
      'scrolls nowhere for a hash that names no card (%s)',
      async (hash) => {
        const { scroll, load } = loadWithHash(hash)

        load()
        await screen.findByRole('heading', { level: 1, name: 'Lê Thu Hà' })

        expect(scroll).not.toHaveBeenCalled()
      },
    )
  })

  it('shows the API’s text of a 404 as the page heading, with the way back to the list', async () => {
    api.fetch.mockRejectedValue(
      refused(404, { type: '/problems/not-found', detail: 'Không tìm thấy dữ liệu yêu cầu.' }),
    )
    renderAccount()

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Không tìm thấy dữ liệu yêu cầu.' }),
    ).toBeVisible()
    expect(screen.getByRole('link', { name: 'Về danh sách nhân viên' })).toHaveAttribute(
      'href',
      '/admin/staff',
    )
    expect(screen.queryByRole('heading', { level: 2 })).not.toBeInTheDocument()
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument()
  })

  it('shows a failed load with "Thử lại", which loads again', async () => {
    api.fetch.mockRejectedValueOnce(refused(500)).mockResolvedValue(ok(manager))
    renderAccount()

    const alert = await screen.findByRole('alert')
    expect(within(alert).getByRole('heading', { level: 1 })).toHaveTextContent(
      'Đã có lỗi xảy ra. Vui lòng thử lại.',
    )
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument()
    expect(screen.queryByRole('region')).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Thử lại' }))
    expect(await screen.findByRole('heading', { level: 1, name: 'Lê Thu Hà' })).toBeVisible()
  })

  it('offers no action without staff.disable, and still says what a lock does', async () => {
    renderAccount(manager.id, ['staff.read'])

    const status = value(await screen.findByRole('region', { name: 'Tài khoản' }), 'Trạng thái')
    expect(
      within(status).getByText(
        'Tài khoản bị khóa không đăng nhập được; khôi phục để nhân viên dùng lại.',
      ),
    ).toBeVisible()
    expect(within(account()).queryByRole('button')).not.toBeInTheDocument()
  })

  it('offers no action on the user’s own account (R-USR-12)', async () => {
    api.fetch.mockResolvedValue(ok({ ...manager, id: ADMIN_ID }))
    renderAccount(ADMIN_ID)

    expect(await screen.findByText('Bạn không thể khóa tài khoản của chính mình.')).toBeVisible()
    expect(within(account()).queryByRole('button')).not.toBeInTheDocument()
  })

  it('offers no action on an anonymized account (R-USR-03)', async () => {
    api.fetch.mockResolvedValue(ok({ ...manager, status: 'ANONYMIZED' }))
    renderAccount()

    expect(
      await screen.findByText(
        'Dữ liệu cá nhân của tài khoản này đã được ẩn danh, nên không khóa hay khôi phục được nữa.',
      ),
    ).toBeVisible()
    expect(within(value(account(), 'Trạng thái')).getByText('Đã ẩn danh')).toHaveAttribute(
      'data-variant',
      'outline',
    )
    expect(within(account()).queryByRole('button')).not.toBeInTheDocument()
  })

  it('offers to restore a disabled account, then to lock it again with why', async () => {
    api.fetch.mockResolvedValue(ok({ ...manager, status: 'DISABLED', leftDate: '2026-11-01' }))
    renderAccount()

    await waitFor(() =>
      expect(within(value(account(), 'Trạng thái')).getByText('Đã khóa')).toHaveAttribute(
        'data-variant',
        'secondary',
      ),
    )
    expect(value(details(), 'Ngày nghỉ việc')).toHaveTextContent('01/11/2026')
    const buttons = within(account()).getAllByRole('button')
    expect(buttons.map((button) => button.textContent)).toEqual(['Khôi phục tài khoản', 'Khóa lại'])
    expect(buttons[1]).toHaveAccessibleDescription(
      'Gửi lại lệnh khóa để đăng xuất nhân viên khỏi mọi thiết bị, khi lần khóa trước báo lỗi.',
    )
  })

  it('places the notice of a PENDING account under the heading, before the cards', async () => {
    api.fetch.mockResolvedValue(ok({ ...manager, status: 'PENDING', mustChangePassword: true }))
    renderAccount()

    const notice = await screen.findByRole('status')
    const heading = screen.getByRole('heading', { level: 1 })
    expect(heading.compareDocumentPosition(notice) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    expect(
      notice.compareDocumentPosition(details()) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy()
    const status = value(account(), 'Trạng thái')
    expect(
      within(status).getByText('Nhân viên chưa đăng nhập được cho tới khi tài khoản được duyệt.'),
    ).toBeVisible()
    expect(within(account()).queryByRole('button')).not.toBeInTheDocument()
  })
})
