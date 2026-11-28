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
import { RequiredMark, RequiredNote } from '@/components/app/required-note'
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

const IDENTIFIER_HINT = 'sign-in-identifier-hint'

/**
 * SCR-AUT-01: staff sign in with their username or company email and their password (UC-001, AUT-01, DEC-145 (4));
 * the card of the page.
 */
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
        <RequiredNote />
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
              name="identifier"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="sign-in-identifier">
                    {t('identifier')}
                    <RequiredMark />
                  </FieldLabel>
                  <Input
                    {...field}
                    id="sign-in-identifier"
                    type="text"
                    autoComplete="username"
                    autoCapitalize="none"
                    spellCheck={false}
                    required
                    placeholder={t('identifierPlaceholder')}
                    aria-describedby={IDENTIFIER_HINT}
                    aria-invalid={fieldState.invalid}
                  />
                  <FieldDescription id={IDENTIFIER_HINT}>{t('identifierHint')}</FieldDescription>
                  {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
                </Field>
              )}
            />
            <Controller
              name="password"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="sign-in-password">
                    {t('password')}
                    <RequiredMark />
                  </FieldLabel>
                  <InputGroup>
                    <InputGroupInput
                      {...field}
                      id="sign-in-password"
                      type={showPassword ? 'text' : 'password'}
                      autoComplete="current-password"
                      required
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
