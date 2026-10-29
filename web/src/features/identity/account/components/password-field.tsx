import type {
  ControllerFieldState,
  ControllerRenderProps,
  FieldValues,
  Path,
} from 'react-hook-form'
import { Field, FieldDescription, FieldError, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'

/** A password field of a form of this module, drawn from the render props of a react-hook-form Controller. */
export function PasswordField<T extends FieldValues, N extends Path<T>>({
  id,
  label,
  autoComplete,
  description,
  field,
  fieldState,
}: {
  id: string
  label: string
  autoComplete: 'new-password' | 'current-password'
  description?: string
  field: ControllerRenderProps<T, N>
  fieldState: ControllerFieldState
}) {
  return (
    <Field data-invalid={fieldState.invalid}>
      <FieldLabel htmlFor={id}>{label}</FieldLabel>
      <Input
        {...field}
        id={id}
        type="password"
        autoComplete={autoComplete}
        aria-describedby={description ? `${id}-help` : undefined}
        aria-invalid={fieldState.invalid}
      />
      {description && <FieldDescription id={`${id}-help`}>{description}</FieldDescription>}
      {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
    </Field>
  )
}
