import { vi } from 'vitest'
import { CurrentUserProvider, type CurrentUser } from '@/features/identity/access'
import type { StaffDto } from '@/lib/api/generated/model'
import { ApiError, type Problem } from '@/lib/api/problems'
import { renderWithProviders } from '@/test/render'
import { StaffAccountProvider } from '../hooks/staff-account-context'
import { StaffAccountDetailsSection } from './staff-account-details-section'
import { StaffAccountHeadingSection } from './staff-account-heading-section'
import { StaffAccountLayout } from './staff-account-layout'
import { StaffAccountStatusSection } from './staff-account-status-section'

// What the tests of SCR-STF-03 share: the sample accounts, the answers of the mocked mutator and the screen as its
// page composes it. Each test file mocks `@/lib/api/api`, `@/lib/auth/session` and `sonner` itself (vi.mock is hoisted
// per file).

/** The Store Manager of the fixtures, active, whose default password is changed. */
export const manager: StaffDto = {
  id: '0199a0f3-0000-7000-8000-000000000002',
  name: 'Quản lý (fixture)',
  email: 'manager.fixture@zengear.test',
  employeeCode: 'FX0001',
  joinedDate: '2026-10-01',
  leftDate: null,
  workPhone: null,
  status: 'ACTIVE',
  mustChangePassword: false,
  roles: [{ id: '0199a000-0000-7000-8000-000000000002', name: 'Store Manager' }],
}

/** The signed-in user: the Admin, whose own account is another one. */
export const ADMIN_ID = '0199a0f3-0000-7000-8000-000000000001'
const admin = (permissions: string[]): CurrentUser => ({
  id: ADMIN_ID,
  name: 'Chủ cửa hàng',
  email: 'owner@zengear.test',
  isStaff: true,
  mustChangePassword: false,
  permissions,
})

/** A 2xx answer of the mutator. */
export const ok = (data: unknown) => ({ data, status: 200, headers: new Headers() })

/** A refused call, as the mutator throws it. */
export const refused = (status: number, problem?: Partial<Problem>) =>
  new ApiError(status, problem ? { status, ...problem } : undefined)

/** The calls the mutator received, as `METHOD path`. */
export const calls = (fetch: ReturnType<typeof vi.fn>) =>
  fetch.mock.calls.map(
    ([url, init]) => `${(init as RequestInit | undefined)?.method ?? 'GET'} ${url}`,
  )

/** The body a call sent. */
export const body = (fetch: ReturnType<typeof vi.fn>, index: number) =>
  JSON.parse(String((fetch.mock.calls[index][1] as RequestInit).body))

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
          details={<StaffAccountDetailsSection />}
          status={<StaffAccountStatusSection />}
        />
      </StaffAccountProvider>
    </CurrentUserProvider>,
  )
}
