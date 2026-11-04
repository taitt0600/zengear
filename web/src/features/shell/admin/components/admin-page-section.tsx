'use client'

import type { ReactNode } from 'react'
import { useForbiddenPage } from '@/features/identity/access'
import { NoPermission } from './no-permission'

/** The page slot of the frame: the no-permission page while a 403 permission-denied holds, else the route's page. */
export function AdminPageSection({ children }: { children: ReactNode }) {
  const { forbidden, clearForbidden } = useForbiddenPage()
  return forbidden ? <NoPermission onLeave={clearForbidden} /> : children
}
