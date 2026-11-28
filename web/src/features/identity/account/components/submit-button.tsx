import { LoaderCircleIcon } from 'lucide-react'
import type { ReactNode } from 'react'
import { Button } from '@/components/ui/button'

/**
 * The submit button of a form of this module, with a spinner while it is sent; `disabled` adds the form's own
 * conditions to `sending`, and `className` its width where the form sets one.
 */
export function SubmitButton({
  sending,
  disabled = sending,
  className,
  children,
}: {
  sending: boolean
  disabled?: boolean
  className?: string
  children: ReactNode
}) {
  return (
    <Button type="submit" disabled={disabled} aria-busy={sending} className={className}>
      {sending && (
        <LoaderCircleIcon data-icon="inline-start" className="motion-safe:animate-spin" />
      )}
      {children}
    </Button>
  )
}
