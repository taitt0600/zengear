'use client'

import { createContext, useContext } from 'react'
import type { CurrentUser } from '../api/me'
import type { SignOut } from './use-staff-session'

/** What the admin layout shares with its sections and pages: the user, the sign-out and the no-permission page. */
export type CurrentUserContextValue = {
  me: CurrentUser
  signOut: SignOut
  // A 403 permission-denied holds for the current path: the page slot shows the no-permission page.
  forbidden: boolean
  clearForbidden: () => void
}

const CurrentUserContext = createContext<CurrentUserContextValue | null>(null)

export const CurrentUserProvider = CurrentUserContext.Provider

function useStaffContext() {
  const context = useContext(CurrentUserContext)
  if (!context) throw new Error('The staff session is used outside the admin layout')
  return context
}

/** The signed-in staff member, inside the admin layout. */
export function useCurrentUser(): CurrentUser {
  return useStaffContext().me
}

/** The sign-out of the admin layout, for a page inside it. */
export function useStaffSignOut(): SignOut {
  return useStaffContext().signOut
}

/** Whether the no-permission page holds the page slot, and how a link leaving it clears it. */
export function useForbiddenPage(): { forbidden: boolean; clearForbidden: () => void } {
  const { forbidden, clearForbidden } = useStaffContext()
  return { forbidden, clearForbidden }
}
