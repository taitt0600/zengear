'use client'

import { createContext, useContext, type ReactNode } from 'react'
import { useStaffList, type StaffList } from './use-staff-list'

const StaffListContext = createContext<StaffList | null>(null)

/**
 * Holds the one call of useStaffList for the sections of SCR-STF-01: the filters and the table read the same URL
 * state, search and query. The route stays a server component (its metadata), so this client provider sits between
 * it and the sections, as the admin layout shares the staff session (identity/access, hooks/current-user.tsx).
 */
export function StaffListProvider({ children }: { children: ReactNode }) {
  return <StaffListContext.Provider value={useStaffList()}>{children}</StaffListContext.Provider>
}

/** The state of the staff list, inside StaffListProvider. */
export function useStaffListState(): StaffList {
  const value = useContext(StaffListContext)
  if (!value) throw new Error('The staff list is used outside StaffListProvider')
  return value
}
