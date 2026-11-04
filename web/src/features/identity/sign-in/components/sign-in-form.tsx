'use client'

import {
  CircleAlertIcon,
  CircleCheckIcon,
  EyeIcon,
  EyeOffIcon,
  LoaderCircleIcon,
} from 'lucide-react'
import { useTranslations } from 'next-intl'
import { useState } from 'react'
import { Controller } from 'react-hook-form'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import {
  InputGroup,
  InputGroupAddon,
  InputGroupButton,
  InputGroupInput,
} from '@/components/ui/input-group'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { useSignIn } from '../hooks/use-sign-in'

const EMAIL_HINT = 'sign-in-email-hint'

/** SCR-AUT-01: staff sign in with their company email and password (UC-001, AUT-01); the card of the page. */
export function SignInForm() {
  const t = useTranslations('signIn')
  const { form, onSubmit, error, passwordChanged, sending } = useSignIn()
  const [showPassword, setShowPassword] = useState(false)

  return (
    <Card>
      <CardHeader className="text-center">
        <CardTitle>
          <h1 className="text-xl">{t('title')}</h1>
        </CardTitle>
        <CardDescription>{t('description')}</CardDescription>
      </CardHeader>
      <CardContent>
        <form noValidate onSubmit={onSubmit}>
          <FieldGroup>
            {passwordChanged && (
              <Alert role="status">
                <CircleCheckIcon aria-hidden />
                <AlertDescription>{t('passwordChanged')}</AlertDescription>
              </Alert>
            )}
            {error && (
              <Alert variant="destructive">
                <CircleAlertIcon aria-hidden />
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}
            <Controller
              name="email"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="sign-in-email">{t('email')}</FieldLabel>
                  <Input
                    {...field}
                    id="sign-in-email"
                    type="email"
                    autoComplete="username"
                    spellCheck={false}
                    placeholder={t('emailPlaceholder')}
                    aria-describedby={EMAIL_HINT}
                    aria-invalid={fieldState.invalid}
                  />
                  <FieldDescription id={EMAIL_HINT}>{t('emailHint')}</FieldDescription>
                  {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
                </Field>
              )}
            />
            <Controller
              name="password"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="sign-in-password">{t('password')}</FieldLabel>
                  <InputGroup>
                    <InputGroupInput
                      {...field}
                      id="sign-in-password"
                      type={showPassword ? 'text' : 'password'}
                      autoComplete="current-password"
                      aria-invalid={fieldState.invalid}
                    />
                    <InputGroupAddon align="inline-end">
                      <Tooltip>
                        <TooltipTrigger asChild>
                          <InputGroupButton
                            size="icon-xs"
                            aria-label={t('showPassword')}
                            aria-pressed={showPassword}
                            onClick={() => setShowPassword((shown) => !shown)}
                          >
                            {showPassword ? <EyeOffIcon aria-hidden /> : <EyeIcon aria-hidden />}
                          </InputGroupButton>
                        </TooltipTrigger>
                        <TooltipContent>{t('showPassword')}</TooltipContent>
                      </Tooltip>
                    </InputGroupAddon>
                  </InputGroup>
                  {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
                </Field>
              )}
            />
            <Field>
              <Button type="submit" className="w-full" disabled={sending} aria-busy={sending}>
                {sending && (
                  <LoaderCircleIcon
                    data-icon="inline-start"
                    className="motion-safe:animate-spin"
                    aria-hidden
                  />
                )}
                {t('submit')}
              </Button>
              <FieldDescription className="text-center">{t('forgotPassword')}</FieldDescription>
            </Field>
          </FieldGroup>
        </form>
      </CardContent>
    </Card>
  )
}
