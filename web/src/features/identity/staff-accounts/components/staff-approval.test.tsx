import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { StaffDto, StaffListItemDto } from '@/lib/api/generated/model'
import { renderWithProviders } from '@/test/render'
import { calls, manager as account, refused, renderAccount } from './staff-account.fixtures'
import { paged, staffListScreen } from './staff-list.fixtures'

// STF-09 on SCR-STF-01 (the tab Chờ duyệt) and SCR-STF-03 (the notice): what the API's flags show, the If-Match of each
// command (DEC-140 (2)), and what follows its answer (R-USR-17, UC-140).

const api = vi.hoisted(() => ({ fetch: vi.fn() }))
vi.mock('@/lib/api/api', () => ({ apiFetch: api.fetch }))
vi.mock('@/lib/auth/session', () => ({}))
const toast = vi.hoisted(() => ({ error: vi.fn(), success: vi.fn() }))
vi.mock('sonner', () => ({ toast }))
vi.mock('@/hooks/use-pending-staff-count', () => ({ usePendingStaffCount: () => 2 }))
const navigation = vi.hoisted(() => ({ search: 'status=PENDING', replace: vi.fn(), push: vi.fn() }))
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: navigation.push, replace: navigation.replace }),
  usePathname: () => '/admin/staff',
  useSearchParams: () => new URLSearchParams(navigation.search),
}))

const creator = { id: '0199a0f3-0000-7000-8000-000000000003', name: 'Phạm Quốc Bảo' }
const CREATED_AT = '2026-11-26T02:00:00Z'

/** A PENDING row with its flags as the API gives them for the viewer. */
const pendingRow = (
  id: string,
  name: string,
  flags: Partial<StaffListItemDto>,
): StaffListItemDto => ({
  id,
  name,
  email: `${id.slice(-4)}@zengear.test`,
  employeeCode: 'ZG260006',
  joinedDate: '2026-11-02',
  leftDate: null,
  status: 'PENDING',
  roles: [{ id: '0199a000-0000-7000-8000-000000000005', name: 'Cashier' }],
  canApprove: false,
  canWithdraw: false,
  createdBy: creator,
  createdAt: CREATED_AT,
  version: '"41"',
  ...flags,
})
const lan = pendingRow('0199a0f3-0000-7000-8000-000000000006', 'Võ Thị Lan', { canApprove: true })
const tung = pendingRow('0199a0f3-0000-7000-8000-000000000007', 'Bùi Văn Tùng', {
  canWithdraw: true,
})
const thu = pendingRow('0199a0f3-0000-7000-8000-000000000010', 'Trịnh Văn Thu', {})

/** The list answers with `rows`; a command answers with 200, 204 or the error it throws. */
function answerList(rows: StaffListItemDto[], command?: unknown) {
  api.fetch.mockImplementation(async (_url: string, init?: RequestInit) => {
    if ((init?.method ?? 'GET') === 'GET') return paged(rows)
    if (command instanceof Error) throw command
    return { data: command, status: command ? 200 : 204, headers: new Headers() }
  })
}

/** The table of the tab once loaded (jsdom also renders the blocks of a narrow screen, which CSS hides). */
const pendingTable = async () => {
  await screen.findByRole('columnheader', { name: 'Người tạo' })
  return screen.getByRole('region', { name: 'Bảng nhân viên' })
}

const ifMatch = (index: number) =>
  new Headers((api.fetch.mock.calls[index][1] as RequestInit).headers).get('If-Match')

beforeEach(() => {
  vi.resetAllMocks()
  navigation.search = 'status=PENDING'
  answerList([lan, tung, thu])
})

describe('the tab Chờ duyệt', () => {
  it('shows the creator and the time, and the actions each row allows', async () => {
    renderWithProviders(staffListScreen(['staff.read', 'staff.create', 'staff.approve']))

    const table = await pendingTable()
    expect(
      within(table)
        .getAllByRole('columnheader')
        .map((h) => h.textContent),
    ).toEqual(['STTSố thứ tự', 'Họ tên', 'Vai trò', 'Người tạo', 'Ngày tạo', 'Thao tác'])
    const [, lanRow, tungRow, thuRow] = within(table).getAllByRole('row')
    expect(lanRow).toHaveTextContent('Phạm Quốc Bảo')
    expect(lanRow).toHaveTextContent('26/11/2026 09:00')
    expect(within(lanRow).getByRole('button', { name: 'Duyệt Võ Thị Lan' })).toBeVisible()
    expect(within(lanRow).getByRole('button', { name: 'Từ chối Võ Thị Lan' })).toBeVisible()
    expect(within(tungRow).getByRole('button', { name: 'Thu hồi Bùi Văn Tùng' })).toBeVisible()
    expect(within(tungRow).queryByRole('button', { name: /^Duyệt/ })).toBeNull()
    expect(within(thuRow).queryAllByRole('button')).toHaveLength(0)
    // WCAG 2.5.3 Label in Name: the link's name starts with the text it shows.
    expect(
      within(thuRow).getByRole('link', { name: 'Xem chi tiết Trịnh Văn Thu' }),
    ).toHaveAttribute('href', `/admin/staff/${thu.id}`)
    expect(screen.getByRole('link', { name: /Chờ duyệt/ })).toHaveAttribute('aria-current', 'page')
  })

  it('approves with the version of the row, then reloads the list', async () => {
    renderWithProviders(staffListScreen(['staff.read', 'staff.approve']))
    await userEvent.click(
      within(await pendingTable()).getByRole('button', { name: 'Duyệt Võ Thị Lan' }),
    )
    const dialog = screen.getByRole('alertdialog', { name: 'Duyệt tài khoản này?' })
    expect(dialog).toHaveTextContent('Phạm Quốc Bảo · 26/11/2026 09:00')
    answerList([tung, thu], { ...lan, status: 'ACTIVE' })

    await userEvent.click(within(dialog).getByRole('button', { name: 'Duyệt' }))

    await waitFor(() => expect(screen.queryByRole('alertdialog')).toBeNull())
    expect(calls(api.fetch)[1]).toBe(`POST /api/v1/admin/staff/${lan.id}/approve`)
    expect(ifMatch(1)).toBe('"41"')
    expect(toast.success).toHaveBeenCalledWith('Đã duyệt tài khoản Võ Thị Lan.', {
      description: 'Nhân viên đăng nhập được bằng mật khẩu mặc định.',
    })
    await waitFor(() => expect(screen.queryAllByText('Võ Thị Lan')).toHaveLength(0))
  })

  it('shows a 412 in a toast, closes and reloads the list (DEC-140 (2))', async () => {
    renderWithProviders(staffListScreen(['staff.read', 'staff.create']))
    await userEvent.click(
      within(await pendingTable()).getByRole('button', { name: 'Thu hồi Bùi Văn Tùng' }),
    )
    answerList(
      [lan, tung, thu],
      refused(412, { type: '/problems/precondition-failed', detail: 'Đã thay đổi.' }),
    )

    await userEvent.click(screen.getByRole('button', { name: 'Thu hồi tài khoản' }))

    await waitFor(() => expect(screen.queryByRole('alertdialog')).toBeNull())
    expect(toast.error).toHaveBeenCalledWith('Đã thay đổi.')
    expect(calls(api.fetch).slice(1)).toEqual([
      `POST /api/v1/admin/staff/${tung.id}/withdraw`,
      'GET /api/v1/admin/staff?status=PENDING&pageSize=20',
    ])
  })

  it('puts the focus on the tab once the last account left it', async () => {
    answerList([lan])
    renderWithProviders(staffListScreen(['staff.read', 'staff.approve']))
    await userEvent.click(
      within(await pendingTable()).getByRole('button', { name: 'Duyệt Võ Thị Lan' }),
    )
    answerList([], { ...lan, status: 'ACTIVE' })

    await userEvent.click(screen.getByRole('button', { name: 'Duyệt' }))

    const empty = await screen.findByText('Không còn tài khoản chờ duyệt.')
    await waitFor(() => expect(empty.closest('[tabindex="-1"]')).toHaveFocus())
  })

  it('says when the search hides every row, and clears only the search', async () => {
    const typing = userEvent.setup()
    renderWithProviders(staffListScreen(['staff.read', 'staff.approve']))
    await pendingTable()

    await typing.type(screen.getByLabelText('Lọc theo tên, mã hoặc email'), 'không có ai')

    expect(screen.getByText('Không có nhân viên phù hợp với bộ lọc.')).toBeVisible()
    expect(screen.queryByText('Không còn tài khoản chờ duyệt.')).toBeNull()
    await typing.click(screen.getByRole('button', { name: 'Xóa bộ lọc' }))
    expect(navigation.push).not.toHaveBeenCalled()
    expect(await screen.findAllByText('Võ Thị Lan')).not.toHaveLength(0)
  })

  it('goes back to page 1 when the last row of a later page left', async () => {
    navigation.search = 'status=PENDING&page=2'
    api.fetch.mockResolvedValue(paged([], { page: 2, totalCount: 20 }))
    renderWithProviders(staffListScreen(['staff.read', 'staff.approve']))

    await waitFor(() =>
      expect(navigation.replace).toHaveBeenCalledWith('/admin/staff?status=PENDING'),
    )
    expect(screen.queryByText('Không có nhân viên phù hợp với bộ lọc.')).toBeNull()
  })

  it('says when no account waits, with the way to every account', async () => {
    answerList([])
    renderWithProviders(staffListScreen(['staff.read', 'staff.approve']))

    expect(await screen.findByText('Không còn tài khoản chờ duyệt.')).toBeVisible()
    expect(screen.getByRole('link', { name: 'Xem tất cả nhân viên' })).toHaveAttribute(
      'href',
      '/admin/staff',
    )
  })
})

/** SCR-STF-03 of a PENDING account with `flags`, loaded with its ETag. */
function answerAccount(flags: Partial<StaffDto>, command?: unknown) {
  const pending: StaffDto = {
    ...account,
    status: 'PENDING',
    createdBy: creator,
    createdAt: CREATED_AT,
    canApprove: false,
    canWithdraw: false,
    ...flags,
  }
  api.fetch.mockImplementation(async (_url: string, init?: RequestInit) => {
    if ((init?.method ?? 'GET') === 'GET')
      return { data: pending, status: 200, headers: new Headers({ ETag: '"7"' }) }
    if (command instanceof Error) throw command
    return { data: command, status: command ? 200 : 204, headers: new Headers() }
  })
}

describe('the notice of a PENDING account', () => {
  it('offers "Từ chối" and "Duyệt" to an approver, with the creator and the time; no status action', async () => {
    answerAccount({ canApprove: true })
    renderAccount(account.id, ['staff.read', 'staff.disable', 'staff.approve'])

    const notice = await screen.findByRole('status')
    expect(notice).toHaveTextContent('Tài khoản đang chờ duyệt')
    expect(notice).toHaveTextContent('Phạm Quốc Bảo tạo lúc 26/11/2026 09:00.')
    expect(notice).toHaveTextContent('Kiểm tra hồ sơ và vai trò bên dưới trước khi duyệt.')
    expect(within(notice).getByRole('button', { name: 'Duyệt' })).toBeVisible()
    expect(screen.queryByRole('button', { name: 'Khóa tài khoản' })).toBeNull()
  })

  it('approves with the ETag, then the status card takes the focus of the notice that went', async () => {
    answerAccount({ canApprove: true })
    renderAccount(account.id, ['staff.read', 'staff.approve'])
    await userEvent.click(
      within(await screen.findByRole('status')).getByRole('button', { name: 'Duyệt' }),
    )
    answerAccount({ canApprove: false, status: 'ACTIVE' }, { ...account, status: 'ACTIVE' })

    await userEvent.click(
      within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Duyệt' }),
    )

    await waitFor(() => expect(screen.queryByRole('status')).toBeNull())
    expect(ifMatch(1)).toBe('"7"')
    await waitFor(() =>
      expect(screen.getByRole('heading', { name: 'Trạng thái tài khoản' })).toHaveFocus(),
    )
  })

  it('rejects with the ETag, then goes to the tab Chờ duyệt', async () => {
    answerAccount({ canApprove: true })
    renderAccount(account.id, ['staff.read', 'staff.approve'])
    await userEvent.click(
      within(await screen.findByRole('status')).getByRole('button', { name: 'Từ chối' }),
    )
    answerAccount({ canApprove: true }, undefined)

    await userEvent.click(screen.getByRole('button', { name: 'Từ chối tài khoản' }))

    await waitFor(() =>
      expect(navigation.replace).toHaveBeenCalledWith('/admin/staff?status=PENDING'),
    )
    const post = calls(api.fetch).indexOf(`POST /api/v1/admin/staff/${account.id}/reject`)
    expect(ifMatch(post)).toBe('"7"')
    expect(toast.success).toHaveBeenCalledWith(`Đã từ chối tài khoản ${account.name}.`)
  })

  it('offers "Thu hồi" to the creator, never the approval', async () => {
    answerAccount({ canWithdraw: true })
    renderAccount(account.id, ['staff.read', 'staff.create'])

    const notice = await screen.findByRole('status')
    expect(notice).toHaveTextContent('Bạn đã tạo tài khoản này nên một người khác sẽ duyệt.')
    expect(within(notice).getByRole('button', { name: 'Thu hồi' })).toBeVisible()
    expect(within(notice).queryByRole('button', { name: 'Duyệt' })).toBeNull()
  })

  it('says another person approves it to anyone else, with no action', async () => {
    answerAccount({})
    renderAccount(account.id, ['staff.read'])

    const notice = await screen.findByRole('status')
    expect(notice).toHaveTextContent('Tài khoản này chờ một người khác duyệt')
    expect(within(notice).queryAllByRole('button')).toHaveLength(0)
  })
})
