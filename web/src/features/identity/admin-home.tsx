'use client'

import { useRouter } from 'next/navigation'
import { useEffect } from 'react'
import { Skeleton } from '@/components/ui/skeleton'
import { landingPath } from './me-outcome'
import { useCurrentUser } from './staff-session'

/** /admin has no screen of its own: it opens the user's landing page (SCR-AUT-01). */
export function AdminHome() {
  const me = useCurrentUser()
  const router = useRouter()
  useEffect(() => {
    router.replace(landingPath(me.permissions))
  }, [me.permissions, router])
  return <Skeleton className="h-7 w-64" />
}
