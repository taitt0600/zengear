'use client'

import { createContext, useContext, type ReactNode } from 'react'
import { useStaffAccount, type StaffAccount } from './use-staff-account'

const StaffAccountContext = createContext<StaffAccount | null>(null)

/**
 * Holds the one call of useStaffAccount for the sections of SCR-STF-03: the heading, the details and the status read
 * the same account. The route stays a server component (its metadata), as for the staff list (staff-list-context.tsx).
 */
export function StaffAccountProvider({ id, children }: { id: string; children: ReactNode }) {
  return (
    <StaffAccountContext.Provider value={useStaffAccount(id)}>
      {children}
    </StaffAccountContext.Provider>
  )
}

/** The state of the staff account, inside StaffAccountProvider. */
export function useStaffAccountState(): StaffAccount {
  const value = useContext(StaffAccountContext)
  if (!value) throw new Error('The staff account is used outside StaffAccountProvider')
  return value
}
