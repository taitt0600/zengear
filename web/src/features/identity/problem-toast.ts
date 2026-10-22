'use client'

import { useTranslations } from 'next-intl'
import { useCallback } from 'react'
import { toast } from 'sonner'
import { ApiError, problemMessage } from '@/lib/api/problems'
import { isNetworkError } from './me-outcome'

/**
 * Shows in a toast a failed call that neither a form field nor a session rule takes (docs/SCREENS.md, Conventions):
 * the message of its problem type, or its `detail`, or the generic text; a network failure says so.
 */
export function useProblemToast(): (error: unknown) => void {
  const t = useTranslations()
  return useCallback(
    (error: unknown) => {
      if (isNetworkError(error)) {
        toast.error(t('signIn.errors.network'))
        return
      }
      const problem = error instanceof ApiError ? error.problem : undefined
      // problemMessage returns a key only when the message file has it; the type names one such key.
      const message = problemMessage(problem, (key) => t.has(key as 'problems.unknown'))
      toast.error('key' in message ? t(message.key as 'problems.unknown') : message.text)
    },
    [t],
  )
}
