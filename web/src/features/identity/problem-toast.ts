'use client'

import { useTranslations } from 'next-intl'
import { useCallback } from 'react'
import { toast } from 'sonner'
import { problemText } from '@/lib/api/problems'

/**
 * The text of a failed call (DEC-117 (4)): the API's `detail`, or the web's own text when there is no answer or the
 * answer has no `detail`.
 */
export function useProblemText(): (error: unknown) => string {
  const t = useTranslations()
  return useCallback(
    (error: unknown) => {
      const text = problemText(error)
      return 'key' in text ? t(text.key) : text.text
    },
    [t],
  )
}

/**
 * Shows in a toast a failed call that neither a form field nor a session rule takes (docs/SCREENS.md, Conventions),
 * with the text of useProblemText.
 */
export function useProblemToast(): (error: unknown) => void {
  const text = useProblemText()
  return useCallback(
    (error: unknown) => {
      toast.error(text(error))
    },
    [text],
  )
}
