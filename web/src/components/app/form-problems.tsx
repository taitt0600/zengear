import { useEffect, useRef } from 'react'

import { Alert, AlertDescription } from '@/components/ui/alert'

/** The texts of a refused form that none of its fields shows, and whether the alert takes the focus. */
export interface FormProblemsState {
  texts: readonly string[]
  focus: boolean
}

export const NO_FORM_PROBLEMS: FormProblemsState = { texts: [], focus: false }

/**
 * The texts of a refused form that none of its fields shows (the `other` of fieldErrors), in an alert at the top of
 * the form: the API's texts, as they come (DEC-117 (4)). Nothing when there are none. When no field shows an error,
 * the alert takes the focus, as the first field in error would.
 */
export function FormProblems({ problems }: { problems: FormProblemsState }) {
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (problems.focus && problems.texts.length > 0) ref.current?.focus()
  }, [problems])

  if (problems.texts.length === 0) return null
  return (
    <Alert ref={ref} variant="destructive" tabIndex={-1}>
      <AlertDescription>
        {problems.texts.map((text) => (
          <p key={text}>{text}</p>
        ))}
      </AlertDescription>
    </Alert>
  )
}
