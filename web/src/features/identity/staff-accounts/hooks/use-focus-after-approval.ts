import { useEffect, useRef, type RefObject } from 'react'
import type { UserStatus } from '@/lib/api/generated/model'

/**
 * An approval from the notice of SCR-STF-03 (STF-09) removes the notice, with the button that had the focus, once the
 * account reloads ACTIVE: `target` (the h2 of the card "Tài khoản") then takes the focus instead of the page's body.
 */
export function useFocusAfterApproval(
  status: UserStatus | undefined,
  target: RefObject<HTMLElement | null>,
) {
  const wasPending = useRef(false)
  useEffect(() => {
    if (wasPending.current && status === 'ACTIVE' && document.activeElement === document.body)
      target.current?.focus()
    wasPending.current = status === 'PENDING'
  }, [status, target])
}
