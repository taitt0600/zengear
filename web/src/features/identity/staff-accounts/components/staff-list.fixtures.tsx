import { vi } from 'vitest'
import { CurrentUserProvider, type CurrentUser } from '@/features/identity/access'
import type { PagedDtoOfStaffListItemDto, StaffListItemDto } from '@/lib/api/generated/model'
import { StaffListProvider } from '../hooks/staff-list-context'
import { StaffListFiltersSection } from './staff-list-filters-section'
import { StaffListHeaderSection } from './staff-list-header-section'
import { StaffListLayout } from './staff-list-layout'
import { StaffListTableSection } from './staff-list-table-section'

// What the tests of SCR-STF-01 share: the sample rows, the answer of the mocked mutator and the screen as its page
// composes it. Each test file mocks `@/lib/api/api`, `@/lib/auth/session` and `next/navigation` itself (vi.mock is
// hoisted per file).

/** The Admin of the seed, active. */
export const owner: StaffListItemDto = {
  id: '0199a0f3-0000-7000-8000-000000000001',
  name: 'Chủ cửa hàng',
  email: 'owner@zengear.test',
  employeeCode: null,
  joinedDate: null,
  leftDate: null,
  status: 'ACTIVE',
  roles: [{ id: '0199a000-0000-7000-8000-000000000001', name: 'Admin' }],
}

/** The Store Manager of the fixtures, disabled with a last working day. */
export const manager: StaffListItemDto = {
  id: '0199a0f3-0000-7000-8000-000000000002',
  name: 'Quản lý (fixture)',
  email: 'manager.fixture@zengear.test',
  employeeCode: 'FX0001',
  joinedDate: '2026-10-01',
  leftDate: '2026-11-01',
  status: 'DISABLED',
  roles: [
    { id: '0199a000-0000-7000-8000-000000000002', name: 'Store Manager' },
    { id: '0199a000-0000-7000-8000-000000000004', name: 'Cashier' },
  ],
}

/** A page of the list, as the mutator resolves it. */
export const paged = (
  items: StaffListItemDto[],
  { page = 1, totalCount = items.length }: { page?: number; totalCount?: number } = {},
) => {
  const data: PagedDtoOfStaffListItemDto = { items, page, pageSize: 20, totalCount }
  return { data, status: 200, headers: new Headers() }
}

/** The id of the signed-in user when a test names none: an account the sample rows do not hold. */
export const USER_ID = '0199a0f3-0000-7000-8000-000000000009'

const user = (permissions: string[], id: string): CurrentUser => ({
  id,
  name: 'Quản lý',
  email: 'manager@zengear.test',
  isStaff: true,
  mustChangePassword: false,
  permissions,
})

/** SCR-STF-01 as its page composes it, for a user who holds `permissions`. */
export const staffListScreen = (permissions: string[], id = USER_ID) => (
  <CurrentUserProvider
    value={{
      me: user(permissions, id),
      signOut: vi.fn(),
      forbidden: false,
      clearForbidden: () => {},
    }}
  >
    <StaffListProvider>
      <StaffListLayout
        header={<StaffListHeaderSection />}
        filters={<StaffListFiltersSection />}
        list={<StaffListTableSection />}
      />
    </StaffListProvider>
  </CurrentUserProvider>
)
