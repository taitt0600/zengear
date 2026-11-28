import { CurrentUserProvider, type LoadedCurrentUser } from '@/features/identity/access'
import { ApiError } from '@/lib/api/problems'
import { renderWithProviders } from '@/test/render'
import { AccountHeadingSection } from './account-heading-section'
import { AccountLayout } from './account-layout'
import { AccountNavSection } from './account-nav-section'
import { AccountPasswordSection } from './account-password-section'
import { AccountProfileSection } from './account-profile-section'

// What the tests of SCR-ACC-01 share: the signed-in staff member and the page as its route composes it. Each test file
// mocks GET /me, the calls of this module and Firebase itself, since vi.mock applies to its own file only.

export const consultant: LoadedCurrentUser = {
  id: '0199a0f3-0000-7000-8000-000000000009',
  name: 'Tư vấn bán hàng',
  email: 'tuvan@zengear.test',
  isStaff: true,
  mustChangePassword: false,
  permissions: [],
  etag: '"v1"',
}

/** A 400 of the API with the given items. */
export const validation = (...errors: { pointer?: string; code: string; detail: string }[]) =>
  new ApiError(400, {
    type: '/problems/validation',
    status: 400,
    detail: 'Vui lòng kiểm tra lại các thông tin đã nhập.',
    errors,
  })

/** The page of SCR-ACC-01, as app/(admin)/admin/(shell)/account/page.tsx composes it. */
export function renderAccountScreen(signOut: () => Promise<void>) {
  return renderWithProviders(
    <CurrentUserProvider
      value={{ me: consultant, signOut, forbidden: false, clearForbidden: () => {} }}
    >
      <AccountLayout
        heading={<AccountHeadingSection />}
        nav={<AccountNavSection />}
        profile={<AccountProfileSection />}
        password={<AccountPasswordSection />}
      />
    </CurrentUserProvider>,
  )
}
