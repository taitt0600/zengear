'use client'

import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslations } from 'next-intl'
import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { toast } from 'sonner'
import type { z } from 'zod'
import { NO_FORM_PROBLEMS } from '@/components/app/form-problems'
import { meQuery, sessionOutcome, type LoadedCurrentUser } from '@/features/identity/access'
import { useProblemToast } from '@/hooks/use-problem-toast'
import { ApiError, fieldErrors } from '@/lib/api/problems'
import { updateMe } from '../api/me'
import { accountSchema } from '../schemas'

/**
 * The display name of SCR-ACC-01, sent with the ETag of the values the form loaded (docs/SCREENS.md, Conventions,
 * Edits with an ETag): a background refetch of GET /me changes neither the field nor that version, so "Lưu" never
 * overwrites a change the form did not show. The form loads again after a 200, a 412 or a 428.
 */
export function useAccountForm(loaded: LoadedCurrentUser) {
  const t = useTranslations()
  const queryClient = useQueryClient()
  const showProblem = useProblemToast()
  const [etag, setEtag] = useState(loaded.etag)
  // The texts of a 400 that no field shows, until the next save.
  const [problems, setProblems] = useState(NO_FORM_PROBLEMS)
  const save = useMutation({ mutationFn: (name: string) => updateMe(name, etag ?? '') })

  const schema = useMemo(
    () =>
      accountSchema({
        required: t('account.validation.nameRequired'),
        tooLong: t('account.validation.nameTooLong'),
      }),
    [t],
  )
  const form = useForm<z.input<typeof schema>, unknown, z.output<typeof schema>>({
    resolver: zodResolver(schema),
    defaultValues: { name: loaded.name },
  })

  function hold(account: LoadedCurrentUser) {
    form.reset({ name: account.name })
    setEtag(account.etag)
  }

  // GET /me again (the header shows the new name from the same query), then the form holds that version. When it
  // fails, the form keeps what was typed and the failure is shown, instead of the version it already had.
  async function reload() {
    try {
      hold(await queryClient.query({ ...meQuery, staleTime: 0 }))
    } catch (error) {
      // A session answer: the admin layout is already signing the user out.
      if (!sessionOutcome(error)) showProblem(error)
    }
  }

  async function onSubmit({ name }: z.output<typeof schema>) {
    let saved: LoadedCurrentUser
    setProblems(NO_FORM_PROBLEMS)
    try {
      saved = await save.mutateAsync(name)
    } catch (error) {
      await showFailure(error)
      return
    }
    // The form holds the answer of PATCH, which a failed GET /me afterwards cannot undo; the header follows GET /me.
    hold(saved)
    void queryClient.invalidateQueries({ queryKey: meQuery.queryKey })
    toast.success(t('account.saved'))
  }

  async function showFailure(error: unknown) {
    // 401 and the session problems: the admin layout follows them (use-staff-session.tsx).
    if (sessionOutcome(error)) return
    if (error instanceof ApiError) {
      // The API's texts (DEC-117 (4)): under the field, and those no field shows at the top of the form.
      if (error.status === 400 && error.problem?.errors?.length) {
        const { byField, other } = fieldErrors(error.problem, ['name'])
        if (byField.name) form.setError('name', { message: byField.name }, { shouldFocus: true })
        setProblems({ texts: other, focus: !byField.name })
        return
      }
      // 428 means the version was missing: reloading gets one, as for a 412. The toast shows the API's text.
      if (error.status === 412 || error.status === 428) {
        showProblem(error)
        await reload()
        return
      }
    }
    showProblem(error)
  }

  return {
    control: form.control,
    problems,
    sending: form.formState.isSubmitting,
    // Without a version nothing can be saved: the API would answer 428.
    canSave: Boolean(etag),
    onSubmit: form.handleSubmit(onSubmit),
  }
}

export type AccountForm = ReturnType<typeof useAccountForm>
