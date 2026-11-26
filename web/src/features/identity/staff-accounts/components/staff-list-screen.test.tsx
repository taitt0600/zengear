import { act, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '@/lib/api/problems'
import { renderWithProviders } from '@/test/render'
import { manager, owner, paged, staffListScreen as staffScreen } from './staff-list.fixtures'

// The mutator of the generated client, which the list's query calls (docs/FRONTEND.md, Tests and checks).
const api = vi.hoisted(() => ({ fetch: vi.fn() }))
vi.mock('@/lib/api/api', () => ({ apiFetch: api.fetch }))
vi.mock('@/lib/auth/session', () => ({}))

// The URL of the page: its search params are what navigation.search holds.
const navigation = vi.hoisted(() => ({
  router: { push: vi.fn(), replace: vi.fn() },
  search: '',
}))
// The count of accounts waiting has its own tests (use-pending-staff-count.test.tsx).
vi.mock('@/hooks/use-pending-staff-count', () => ({ usePendingStaffCount: () => undefined }))
vi.mock('next/navigation', () => ({
  useRouter: () => navigation.router,
  usePathname: () => '/admin/staff',
  useSearchParams: () => new URLSearchParams(navigation.search),
}))

// jsdom has neither, and the Select of Radix uses both when it opens. scrollIntoView answers a Promise, as Chromium's
// does, so that an effect returning it (React takes it for the clean-up and the page fails) fails here too.
beforeAll(() => {
  window.HTMLElement.prototype.hasPointerCapture ??= () => false
  window.HTMLElement.prototype.scrollIntoView ??= (() =>
    Promise.resolve()) as unknown as HTMLElement['scrollIntoView']
})

// The screen as its page composes it.
const renderScreen = (permissions = ['staff.read', 'staff.create']) =>
  renderWithProviders(staffScreen(permissions))

const table = () => screen.findByRole('region', { name: 'Bảng nhân viên' })
// The list once loaded: the region also holds the skeleton rows while it loads.
const loaded = async () => {
  await screen.findByRole('link', { name: 'Trần Minh Khoa' })
  return table()
}
const searchBox = () => screen.getByLabelText('Lọc theo tên, mã hoặc email')
const pagination = () => screen.getByRole('navigation', { name: 'Phân trang' })

beforeEach(() => {
  vi.resetAllMocks()
  navigation.search = ''
  api.fetch.mockResolvedValue(paged([owner, manager]))
})

describe('the staff list', () => {
  it('shows each account with its roles, status and dates', async () => {
    renderScreen()

    const rows = within(await loaded()).getAllByRole('row')
    expect(rows).toHaveLength(3)
    const [, ownerCells, managerCells] = rows.map((row) => within(row).queryAllByRole('cell'))
    expect(within(rows[1]).getByRole('link', { name: 'Trần Minh Khoa' })).toHaveAttribute(
      'href',
      `/admin/staff/${owner.id}`,
    )
    // STT first, then the name with the email under it (STF-09), the other columns of the API, then the row's actions.
    expect(ownerCells).toHaveLength(8)
    expect(ownerCells[0]).toHaveTextContent(/^1$/)
    expect(managerCells[0]).toHaveTextContent(/^2$/)
    expect(ownerCells[1]).toHaveTextContent('khoatmzg260001@zengear.test')
    expect(ownerCells[2]).toHaveTextContent('—')
    expect(ownerCells[4]).toHaveTextContent('Đang hoạt động')
    expect(ownerCells[5]).toHaveTextContent('—')
    expect(ownerCells[6]).toHaveTextContent(/^$/)
    expect(within(managerCells[3]).getByText('Store Manager')).toBeVisible()
    expect(within(managerCells[3]).getByText('Cashier')).toBeVisible()
    expect(managerCells[4]).toHaveTextContent('Đã khóa')
    expect(managerCells[5]).toHaveTextContent('01/10/2026')
    expect(managerCells[6]).toHaveTextContent('01/11/2026')
    expect(
      within(ownerCells[7]).getByRole('button', { name: 'Thao tác cho Trần Minh Khoa' }),
    ).toBeVisible()
    // The nine headers in order, by their names: "STT" shows, "Số thứ tự" is what a screen reader reads.
    const headers = [
      'Số thứ tự',
      'Họ tên',
      'Mã nhân viên',
      'Vai trò',
      'Trạng thái',
      'Ngày vào làm',
      'Ngày nghỉ việc',
      'Thao tác',
    ].map((name) => screen.getByRole('columnheader', { name }))
    expect(screen.getAllByRole('columnheader')).toEqual(headers)
    expect(within(headers[0]).getByText('STT')).toHaveAttribute('aria-hidden', 'true')

    expect(screen.getByRole('heading', { level: 1, name: 'Nhân viên' })).toBeVisible()
    expect(screen.getByRole('link', { name: 'Thêm nhân viên' })).toHaveAttribute(
      'href',
      '/admin/staff/new',
    )
    expect(screen.getByText('2 nhân viên')).toBeVisible()
    expect(within(pagination()).getByText('Trang 1 / 1')).toBeVisible()
    for (const name of ['Trang trước', 'Trang sau']) {
      const link = within(pagination()).getByRole('link', { name })
      expect(link).toHaveAttribute('aria-disabled', 'true')
      expect(link).not.toHaveAttribute('href')
      // A link that leads nowhere takes neither the pointer nor the focus, so it has no tooltip to open.
      expect(link).not.toHaveAttribute('data-state')
    }
  })

  it('keeps the loaded list when a later refresh fails', async () => {
    const { client } = renderScreen()

    await loaded()
    api.fetch.mockRejectedValueOnce(new TypeError('Failed to fetch'))
    await client.refetchQueries()
    // The failure reaches the screen through the query cache's notifications, a task later.
    await act(() => new Promise((resolve) => setTimeout(resolve, 0)))
    expect(client.getQueryCache().getAll()[0].state.status).toBe('error')
    expect(screen.getByRole('link', { name: 'Trần Minh Khoa' })).toBeVisible()
    expect(screen.queryByRole('alert')).toBeNull()
  })

  it("asks the API for the URL's status, sort and page, 20 rows at a time", async () => {
    navigation.search = 'status=DISABLED&sort=-joinedDate&page=2'
    renderScreen()

    await waitFor(() =>
      expect(api.fetch).toHaveBeenCalledWith(
        '/api/v1/admin/staff?status=DISABLED&sort=-joinedDate&page=2&pageSize=20',
        expect.anything(),
      ),
    )
  })

  it('asks for the first page sorted by name when the URL has nothing', async () => {
    renderScreen()

    await waitFor(() =>
      expect(api.fetch).toHaveBeenCalledWith('/api/v1/admin/staff?pageSize=20', expect.anything()),
    )
  })

  it('sorts by a column, then the other way, back on page 1', async () => {
    const clicks = userEvent.setup()
    navigation.search = 'page=2'
    renderScreen()

    const name = await screen.findByRole('columnheader', { name: 'Họ tên' })
    expect(name).toHaveAttribute('aria-sort', 'ascending')
    const code = screen.getByRole('columnheader', { name: 'Mã nhân viên' })
    expect(code).not.toHaveAttribute('aria-sort')
    await clicks.click(within(name).getByRole('button', { name: 'Họ tên' }))
    expect(navigation.router.push).toHaveBeenLastCalledWith('/admin/staff?sort=-name', {
      scroll: false,
    })
    await clicks.click(within(code).getByRole('button', { name: 'Mã nhân viên' }))
    expect(navigation.router.push).toHaveBeenLastCalledWith('/admin/staff?sort=employeeCode', {
      scroll: false,
    })
  })

  it('shows the descending sort of the URL', async () => {
    navigation.search = 'sort=-joinedDate'
    renderScreen()

    expect(await screen.findByRole('columnheader', { name: 'Ngày vào làm' })).toHaveAttribute(
      'aria-sort',
      'descending',
    )
    expect(screen.getByRole('columnheader', { name: 'Họ tên' })).not.toHaveAttribute('aria-sort')
  })

  it('reads an empty value of the URL as no value', async () => {
    navigation.search = 'status=&sort=&page='
    api.fetch.mockResolvedValue(paged([]))
    renderScreen()

    expect(await screen.findByText('Chưa có tài khoản nhân viên.')).toBeVisible()
    expect(api.fetch).toHaveBeenCalledWith('/api/v1/admin/staff?pageSize=20', expect.anything())
    expect(screen.getByRole('link', { name: 'Tất cả' })).toHaveAttribute('aria-current', 'page')
  })

  it('shows the default sort when the URL leaves it empty', async () => {
    navigation.search = 'sort='
    renderScreen()

    expect(await screen.findByRole('columnheader', { name: 'Họ tên' })).toHaveAttribute(
      'aria-sort',
      'ascending',
    )
  })

  it('links each status, dropping the page and the sort (STF-09)', async () => {
    navigation.search = 'page=3&sort=-name'
    renderScreen()

    const nav = await screen.findByRole('navigation', { name: 'Trạng thái' })
    const links = within(nav).getAllByRole('link')
    expect(links.map((link) => [link.textContent, link.getAttribute('href')])).toEqual([
      ['Tất cả', '/admin/staff'],
      ['Đang hoạt động', '/admin/staff?status=ACTIVE'],
      ['Đã khóa', '/admin/staff?status=DISABLED'],
      ['Chờ duyệt', '/admin/staff?status=PENDING'],
    ])
    expect(links[0]).toHaveAttribute('aria-current', 'page')
  })

  it('filters the loaded rows by name, code or email without asking the API', async () => {
    const typing = userEvent.setup()
    renderScreen()

    await loaded()
    await typing.type(searchBox(), 'zg260002')
    expect(screen.queryByRole('link', { name: 'Trần Minh Khoa' })).toBeNull()
    expect(screen.getByRole('link', { name: 'Lê Thu Hà' })).toBeVisible()
    expect(api.fetch).toHaveBeenCalledTimes(1)
    expect(searchBox()).toHaveAttribute('placeholder', 'an@example.com')
    expect(searchBox()).toHaveAttribute('name', 'search')
    expect(searchBox()).toHaveAttribute('autocomplete', 'off')
    expect(searchBox()).toHaveAttribute('spellcheck', 'false')
    expect(searchBox()).not.toHaveAccessibleDescription()
  })

  it('numbers the rows from the page, and the search hides rows without renumbering', async () => {
    const typing = userEvent.setup()
    navigation.search = 'page=2'
    api.fetch.mockResolvedValue(paged([owner, manager], { page: 2, totalCount: 22 }))
    renderScreen()

    const number = (name: string) => {
      const row = screen.getAllByRole('row').find((r) => within(r).queryByRole('link', { name }))
      return within(row!).getAllByRole('cell')[0]
    }
    await loaded()
    expect(number('Trần Minh Khoa')).toHaveTextContent(/^21$/)
    expect(number('Lê Thu Hà')).toHaveTextContent(/^22$/)
    await typing.type(searchBox(), 'zg260002')
    expect(number('Lê Thu Hà')).toHaveTextContent(/^22$/)
  })

  it('keeps the hint of the search while the next page loads', async () => {
    navigation.search = 'page=2'
    api.fetch.mockResolvedValue(paged([owner], { page: 2, totalCount: 45 }))
    const { rerender } = renderScreen()

    await loaded()
    navigation.search = 'page=3'
    api.fetch.mockReturnValue(new Promise(() => {}))
    rerender(staffScreen(['staff.read']))
    expect(await table()).toHaveAttribute('aria-busy', 'true')
    expect(searchBox()).toHaveAccessibleDescription('Chỉ lọc trong trang đang xem.')
  })

  it('keeps the pages when the search hides every row of this one', async () => {
    const typing = userEvent.setup()
    navigation.search = 'page=2'
    api.fetch.mockResolvedValue(paged([owner], { page: 2, totalCount: 45 }))
    renderScreen()

    await loaded()
    await typing.type(searchBox(), 'không có ai')
    expect(screen.getByText('Không có nhân viên phù hợp với bộ lọc.')).toBeVisible()
    expect(within(pagination()).getByRole('link', { name: 'Trang sau' })).toHaveAttribute(
      'href',
      '/admin/staff?page=3',
    )
  })

  it('says the search keeps to the page when there are more pages, and links them', async () => {
    navigation.search = 'page=2&sort=-name&status=ACTIVE'
    api.fetch.mockResolvedValue(paged([owner], { page: 2, totalCount: 45 }))
    renderScreen()

    await loaded()
    expect(searchBox()).toHaveAccessibleDescription('Chỉ lọc trong trang đang xem.')
    expect(screen.getByText('45 nhân viên')).toBeVisible()
    expect(within(pagination()).getByText('Trang 2 / 3')).toBeVisible()
    expect(within(pagination()).getByRole('link', { name: 'Trang trước' })).toHaveAttribute(
      'href',
      '/admin/staff?sort=-name&status=ACTIVE',
    )
    expect(within(pagination()).getByRole('link', { name: 'Trang sau' })).toHaveAttribute(
      'href',
      '/admin/staff?page=3&sort=-name&status=ACTIVE',
    )
  })

  it('says when the search hides every row, and clears it without leaving the page', async () => {
    const typing = userEvent.setup()
    navigation.search = 'page=2'
    api.fetch.mockResolvedValue(paged([owner, manager], { page: 2, totalCount: 22 }))
    renderScreen()

    await loaded()
    await typing.type(searchBox(), 'không có ai')
    expect(screen.getByText('Không có nhân viên phù hợp với bộ lọc.')).toBeVisible()
    expect(screen.getByText('Thử bỏ bớt điều kiện lọc để xem thêm tài khoản.')).toBeVisible()
    await typing.click(screen.getByRole('button', { name: 'Xóa bộ lọc' }))
    expect(searchBox()).toHaveValue('')
    expect(navigation.router.push).not.toHaveBeenCalled()
    expect(await screen.findByRole('link', { name: 'Trần Minh Khoa' })).toBeVisible()
  })

  it('says when no account has the status, and clears the filter', async () => {
    const clicks = userEvent.setup()
    navigation.search = 'status=DISABLED&sort=-name'
    api.fetch.mockResolvedValue(paged([]))
    renderScreen()

    expect(await screen.findByText('Không có nhân viên phù hợp với bộ lọc.')).toBeVisible()
    await clicks.click(screen.getByRole('button', { name: 'Xóa bộ lọc' }))
    expect(navigation.router.push).toHaveBeenLastCalledWith('/admin/staff?sort=-name', {
      scroll: false,
    })
  })

  it('says when there is no staff account, with the way to add one', async () => {
    api.fetch.mockResolvedValue(paged([]))
    renderScreen()

    expect(await screen.findByText('Chưa có tài khoản nhân viên.')).toBeVisible()
    expect(
      screen.getByText('Tài khoản do quản trị viên hoặc quản lý cửa hàng tạo sẽ hiện ở đây.'),
    ).toBeVisible()
    expect(screen.getAllByRole('link', { name: 'Thêm nhân viên' })).toHaveLength(2)
  })

  it('offers no way to add an account without staff.create', async () => {
    api.fetch.mockResolvedValue(paged([]))
    renderScreen(['staff.read'])

    expect(await screen.findByText('Chưa có tài khoản nhân viên.')).toBeVisible()
    expect(screen.queryByRole('link', { name: 'Thêm nhân viên' })).toBeNull()
  })

  it('falls back to the default filters when the API refuses the URL', async () => {
    navigation.search = 'status=LOCKED'
    api.fetch.mockRejectedValue(
      new ApiError(400, {
        type: '/problems/validation',
        status: 400,
        detail: 'Vui lòng kiểm tra lại các thông tin đã nhập.',
        errors: [
          { parameter: 'status', code: 'STATUS_INVALID', detail: 'Trạng thái không hợp lệ.' },
        ],
      }),
    )
    renderScreen()

    await waitFor(() => expect(navigation.router.replace).toHaveBeenCalledWith('/admin/staff'))
  })

  it('shows a 400 when the URL has nothing left to reset', async () => {
    api.fetch.mockRejectedValue(
      new ApiError(400, {
        type: '/problems/validation',
        status: 400,
        detail: 'Yêu cầu không hợp lệ.',
      }),
    )
    renderScreen()

    expect(await screen.findByRole('alert')).toHaveTextContent('Yêu cầu không hợp lệ.')
    expect(navigation.router.replace).not.toHaveBeenCalled()
  })

  it('goes back to page 1 from a page past the end', async () => {
    navigation.search = 'page=9&status=ACTIVE'
    api.fetch.mockResolvedValue(paged([], { page: 9, totalCount: 2 }))
    renderScreen()

    await waitFor(() =>
      expect(navigation.router.replace).toHaveBeenCalledWith('/admin/staff?status=ACTIVE'),
    )
  })

  it.each([
    ['an answer without a detail', new ApiError(500), 'Đã có lỗi xảy ra. Vui lòng thử lại.'],
    [
      'no answer',
      new TypeError('Failed to fetch'),
      'Không kết nối được máy chủ. Vui lòng kiểm tra mạng và thử lại.',
    ],
  ])('shows %s in place of the rows, and tries again', async (_, error, text) => {
    const clicks = userEvent.setup()
    api.fetch.mockRejectedValueOnce(error)
    renderScreen()

    expect(await screen.findByRole('alert')).toHaveTextContent(text)
    expect(searchBox()).toBeVisible()
    await clicks.click(screen.getByRole('button', { name: 'Thử lại' }))
    expect(await screen.findByRole('link', { name: 'Trần Minh Khoa' })).toBeVisible()
    expect(api.fetch).toHaveBeenCalledTimes(2)
  })

  it('shows skeleton rows while the list loads', async () => {
    api.fetch.mockReturnValue(new Promise(() => {}))
    renderScreen()

    expect(await table()).toHaveAttribute('aria-busy', 'true')
    expect(screen.getByRole('columnheader', { name: 'Họ tên' })).toBeVisible()
    expect(screen.queryByRole('link', { name: 'Trần Minh Khoa' })).toBeNull()
    // One bar a column, the eight of the table.
    for (const row of within(await table())
      .getAllByRole('row')
      .slice(1))
      expect(within(row).getAllByRole('cell')).toHaveLength(8)
  })
})
