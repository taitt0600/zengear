'use client'

import { CircleAlertIcon, LoaderCircleIcon } from 'lucide-react'
import { useTranslations } from 'next-intl'
import { useId } from 'react'
import { FormProblems } from '@/components/app/form-problems'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {
  Field,
  FieldContent,
  FieldDescription,
  FieldGroup,
  FieldLabel,
} from '@/components/ui/field'
import { useDisableForm, type DisableAccount } from '../hooks/use-disable-form'
import { StaffLeftDateField } from './staff-left-date-field'

/**
 * The dialog "Khóa tài khoản" of SCR-STF-03 (STF-02; R-USR-07, R-USR-12, R-SEC-04), which a row of SCR-STF-01 opens
 * too: the warning that the person is signed out everywhere, whether they left with their last working day, and
 * "Khóa". Its content mounts at each opening, so it starts from the account's values every time.
 */
export function StaffDisableDialog({
  account,
  open,
  onOpenChange,
}: {
  account: DisableAccount
  open: boolean
  onOpenChange: (open: boolean) => void
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DisableForm account={account} onDone={() => onOpenChange(false)} />
      </DialogContent>
    </Dialog>
  )
}

function DisableForm({ account, onDone }: { account: DisableAccount; onDone: () => void }) {
  const t = useTranslations('staff.account')
  const { form, submit, problems, setLeaving, today, joinsLater, pending } = useDisableForm(
    account,
    onDone,
  )
  const leaving = form.watch('leaving')
  const leftDate = form.watch('leftDate')
  const id = useId()

  return (
    <form onSubmit={submit} noValidate className="flex flex-col gap-4">
      <DialogHeader>
        <DialogTitle>{t('disableDialog.title')}</DialogTitle>
        <DialogDescription>{t('disableDialog.description')}</DialogDescription>
      </DialogHeader>
      <Alert variant="destructive">
        <CircleAlertIcon aria-hidden />
        <AlertDescription>{t('disableDialog.warning')}</AlertDescription>
      </Alert>
      <FormProblems problems={problems} />
      <FieldGroup>
        <Field orientation="horizontal" data-disabled={joinsLater ? true : undefined}>
          <Checkbox
            id={id}
            checked={leaving}
            disabled={joinsLater}
            onCheckedChange={(checked) => setLeaving(checked === true)}
            aria-describedby={`${id}-hint`}
          />
          <FieldContent>
            <FieldLabel htmlFor={id}>{t('disableDialog.leaving')}</FieldLabel>
            <FieldDescription id={`${id}-hint`}>
              {joinsLater ? t('disableDialog.leftDateFuture') : t('disableDialog.leavingHint')}
            </FieldDescription>
          </FieldContent>
        </Field>
        {leaving && leftDate && (
          <StaffLeftDateField
            value={leftDate}
            onChange={(value) => form.setValue('leftDate', value)}
            today={today}
            joinedDate={account.joinedDate}
            error={form.formState.errors.leftDate?.message}
          />
        )}
      </FieldGroup>
      <DialogFooter>
        <DialogClose asChild>
          <Button type="button" variant="outline">
            {t('cancel')}
          </Button>
        </DialogClose>
        <Button type="submit" variant="destructive-soft" disabled={pending}>
          {pending && (
            <LoaderCircleIcon className="animate-spin" data-icon="inline-start" aria-hidden />
          )}
          {t('disableDialog.submit')}
        </Button>
      </DialogFooter>
    </form>
  )
}
