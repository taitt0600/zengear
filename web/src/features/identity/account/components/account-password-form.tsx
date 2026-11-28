import { useTranslations } from 'next-intl'
import { useId } from 'react'
import { Controller, useWatch } from 'react-hook-form'
import { FormProblems } from '@/components/app/form-problems'
import { FieldGroup } from '@/components/ui/field'
import { useChangePassword } from '../hooks/use-change-password'
import { passwordRuleStates } from '../utils/password-rules'
import { AccountName } from './account-name'
import { PasswordField } from './password-field'
import { PasswordRules, type PasswordRule } from './password-rules'
import { SubmitButton } from './submit-button'

/**
 * The password form of SCR-ACC-01 on the page (its Layout, Password card): the current password, which Firebase checks
 * before the send, then the new password typed twice, each with its checklist of R-USR-11, which only guides: the
 * length and the repeat block the send, as the schema says, and the API decides the rest (DEC-117 (4)). After the
 * change `onChanged` signs the user out (DEC-094). `email` names the account to password managers.
 */
export function AccountPasswordForm({
  email,
  onChanged,
}: {
  email: string | null | undefined
  onChanged: () => Promise<void>
}) {
  const t = useTranslations()
  const id = useId()
  const { control, problems, sending, onSubmit } = useChangePassword(onChanged, {
    currentPassword: true,
  })
  const [newPassword = '', confirmation = ''] = useWatch({
    control,
    name: ['newPassword', 'confirmation'],
  })
  const states = passwordRuleStates(newPassword, confirmation)
  const rules: PasswordRule[] = [
    { id: `${id}-length`, text: t('account.passwordRules.length'), state: states.length },
    { id: `${id}-zengear`, text: t('account.passwordRules.noZengear'), state: states.noZengear },
    { id: `${id}-server`, text: t('account.passwordRules.serverChecked'), state: 'neutral' },
  ]
  const match: PasswordRule = {
    id: `${id}-match`,
    text: t('account.passwordRules.match'),
    state: states.match,
  }

  return (
    <form noValidate onSubmit={onSubmit} className="max-w-md">
      <FieldGroup>
        {email && <AccountName email={email} />}
        <FormProblems problems={problems} />
        <Controller
          name="currentPassword"
          control={control}
          render={({ field, fieldState }) => (
            <PasswordField
              id={`${id}-current`}
              label={t('changePassword.reauth.password')}
              autoComplete="current-password"
              required
              field={field}
              fieldState={fieldState}
            />
          )}
        />
        <Controller
          name="newPassword"
          control={control}
          render={({ field, fieldState }) => (
            <PasswordField
              id={`${id}-new`}
              label={t('changePassword.newPassword')}
              autoComplete="new-password"
              required
              describedBy={rules.map((rule) => rule.id).join(' ')}
              field={field}
              fieldState={fieldState}
            >
              <PasswordRules rules={rules} heading={t('account.passwordRules.label')} />
            </PasswordField>
          )}
        />
        <Controller
          name="confirmation"
          control={control}
          render={({ field, fieldState }) => (
            <PasswordField
              id={`${id}-confirmation`}
              label={t('changePassword.confirmation')}
              autoComplete="new-password"
              required
              describedBy={match.id}
              field={field}
              fieldState={fieldState}
            >
              <PasswordRules rules={[match]} />
            </PasswordField>
          )}
        />
        <div>
          <SubmitButton sending={sending} className="w-full sm:w-auto">
            {t('account.changePassword')}
          </SubmitButton>
        </div>
      </FieldGroup>
    </form>
  )
}
