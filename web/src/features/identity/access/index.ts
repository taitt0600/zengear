// The public API of the module Access (DEC-118 (2)): the staff session (AUT-04) and what the sign-in (AUT-01) reads.
export { meQuery, type CurrentUser, type LoadedCurrentUser } from './api/me'
export {
  CurrentUserProvider,
  useCurrentUser,
  useStaffSession,
  useStaffSignOut,
  type StaffSession,
} from './hooks/use-staff-session'
export {
  firebaseError,
  landingPath,
  meOutcome,
  sessionOutcome,
  SIGN_OUT_REASONS,
  type MeOutcome,
  type PasswordSignInError,
  type SignOutReason,
} from './utils/me-outcome'
export { adminRoutes, PASSWORD_CHANGED, safeNext } from './utils/routes'
export { takeSignOutReason } from './utils/sign-out-reason'
