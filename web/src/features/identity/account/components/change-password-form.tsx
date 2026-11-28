import { useTranslations } from 'next-intl'
import { useId } from 'react'
import { Controller } from 'react-hook-form'
import { FormProblems } from '@/components/app/form-problems'
import { FieldGroup } from '@/components/ui/field'
import { useChangePassword } from '../hooks/use-change-password'
import { AccountName } from './account-name'
import { PasswordField } from './password-field'
import { ReauthDialog } from './reauth-dialog'
import { SubmitButton } from './submit-button'

/**
 * The form of SCR-AUT-02 (docs/screens/): the new password typed twice, sent to POST /me/change-password (DEC-094),
 * with the sign-in again of 403 recent-login-required in a dialog. After a 204 every session is revoked, so
 * `onChanged` signs the user out. `email` names the account to password managers in a field the user does not see.
 */
export function ChangePasswordForm({
  email,
  onChanged,
}: {
  email: string | null | undefined
  onChanged: () => Promise<void>
}) {
  const t = useTranslations('changePassword')
  const id = useId()
  const { control, problems, sending, onSubmit, reauth } = useChangePassword(onChanged)

  return (
    <>
      <form noValidate onSubmit={onSubmit}>
        <FieldGroup>
          {email && <AccountName email={email} />}
          <FormProblems problems={problems} />
          <Controller
            name="newPassword"
            control={control}
            render={({ field, fieldState }) => (
              <PasswordField
                id={`${id}-new`}
                label={t('newPassword')}
                autoComplete="new-password"
                description={t('help')}
                field={field}
                fieldState={fieldState}
              />
            )}
          />
          <Controller
            name="confirmation"
            control={control}
            render={({ field, fieldState }) => (
              <PasswordField
                id={`${id}-confirmation`}
                label={t('confirmation')}
                autoComplete="new-password"
                field={field}
                fieldState={fieldState}
              />
            )}
          />
          <div className="flex justify-end gap-2">
            <SubmitButton sending={sending}>{t('submit')}</SubmitButton>
          </div>
        </FieldGroup>
      </form>
      <ReauthDialog email={email} {...reauth} />
    </>
  )
}
