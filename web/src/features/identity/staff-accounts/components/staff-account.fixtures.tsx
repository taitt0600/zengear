import { vi } from 'vitest'
import { CurrentUserProvider, type CurrentUser } from '@/features/identity/access'
import type { StaffDto } from '@/lib/api/generated/model'
import { ApiError, type Problem } from '@/lib/api/problems'
import { renderWithProviders } from '@/test/render'
import { StaffAccountProvider } from '../hooks/staff-account-context'
import { StaffAccountDetailsSection } from './staff-account-details-section'
import { StaffAccountHeadingSection } from './staff-account-heading-section'
import { StaffAccountLayout } from './staff-account-layout'
import { StaffAccountLoginSection } from './staff-account-login-section'
import { StaffAccountNavSection } from './staff-account-nav-section'
import { StaffAccountNoticeSection } from './staff-account-notice-section'

// What the tests of SCR-STF-03 share: the sample accounts, the answers of the mocked mutator and the screen as its
// page composes it. Each test file mocks `@/lib/api/api`, `@/lib/auth/session` and `sonner` itself (vi.mock is hoisted
// per file).

/** The Store Manager of the sample cast (DEC-146), active, whose default password is changed. */
export const manager: StaffDto = {
  id: '0199a0f3-0000-7000-8000-000000000002',
  name: 'Lê Thu Hà',
  email: 'haltzg260002@zengear.test',
  username: 'HaLTZG260002',
  employeeCode: 'ZG260002',
  joinedDate: '2026-10-01',
  leftDate: null,
  workPhone: null,
  status: 'ACTIVE',
  mustChangePassword: false,
  roles: [{ id: '0199a000-0000-7000-8000-000000000002', name: 'Store Manager' }],
  canApprove: null,
  canWithdraw: null,
  createdBy: null,
  createdAt: null,
}

/** The signed-in user: the Admin, whose own account is another one. */
export const ADMIN_ID = '0199a0f3-0000-7000-8000-000000000001'
const admin = (permissions: string[]): CurrentUser => ({
  id: ADMIN_ID,
  name: 'Trần Minh Khoa',
  email: 'khoatmzg260001@zengear.test',
  isStaff: true,
  mustChangePassword: false,
  permissions,
})

/** A 2xx answer of the mutator. */
export const ok = (data: unknown) => ({ data, status: 200, headers: new Headers() })

/** A refused call, as the mutator throws it. */
export const refused = (status: number, problem?: Partial<Problem>) =>
  new ApiError(status, problem ? { status, ...problem } : undefined)

/** The query of the count of accounts waiting (STF-09), which the status links and the menu send on their own. */
const PENDING_COUNT = '/api/v1/admin/staff?status=PENDING&pageSize=1'

/** The calls of the screen under test, the count's left out (its own tests count it). */
const screenCalls = (fetch: ReturnType<typeof vi.fn>) =>
  fetch.mock.calls.filter(([url]) => url !== PENDING_COUNT)

/** The calls the mutator received, as `METHOD path`, the count's left out. */
export const calls = (fetch: ReturnType<typeof vi.fn>) =>
  screenCalls(fetch).map(
    ([url, init]) => `${(init as RequestInit | undefined)?.method ?? 'GET'} ${url}`,
  )

/** The body a call sent, by its place among `calls`. */
export const body = (fetch: ReturnType<typeof vi.fn>, index: number) =>
  JSON.parse((screenCalls(fetch)[index][1] as RequestInit).body as string)

/** SCR-STF-03 as its page composes it, for the account `id`. */
export function renderAccount(id = manager.id, permissions = ['staff.read', 'staff.disable']) {
  return renderWithProviders(
    <CurrentUserProvider
      value={{
        me: admin(permissions),
        signOut: vi.fn(),
        forbidden: false,
        clearForbidden: () => {},
      }}
    >
      <StaffAccountProvider id={id}>
        <StaffAccountLayout
          heading={<StaffAccountHeadingSection />}
          notice={<StaffAccountNoticeSection />}
          nav={<StaffAccountNavSection />}
          details={<StaffAccountDetailsSection />}
          account={<StaffAccountLoginSection />}
        />
      </StaffAccountProvider>
    </CurrentUserProvider>,
  )
}
