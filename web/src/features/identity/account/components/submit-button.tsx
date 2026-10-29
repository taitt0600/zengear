import { LoaderCircleIcon } from 'lucide-react'
import type { ReactNode } from 'react'
import { Button } from '@/components/ui/button'

/**
 * The submit button of a form of this module, with a spinner while it is sent; `disabled` adds the form's own
 * conditions to `sending`.
 */
export function SubmitButton({
  sending,
  disabled = sending,
  children,
}: {
  sending: boolean
  disabled?: boolean
  children: ReactNode
}) {
  return (
    <Button type="submit" disabled={disabled} aria-busy={sending}>
      {sending && (
        <LoaderCircleIcon data-icon="inline-start" className="motion-safe:animate-spin" />
      )}
      {children}
    </Button>
  )
}
