import { z } from 'zod'

// The input shape of SCR-AUT-01 (docs/screens/SCR-AUT-01.md). Firebase and the API decide the rest (UC-001).

const EMAIL = z.email()

/**
 * The username or company email, trimmed, where a value with `@` must be an email, and the password exactly as typed:
 * Firebase compares what is typed (DEC-102).
 */
export function signInSchema(messages: {
  identifierRequired: string
  emailInvalid: string
  passwordRequired: string
}) {
  return z.object({
    identifier: z
      .string()
      .trim()
      .min(1, { error: messages.identifierRequired })
      .refine((value) => !value.includes('@') || EMAIL.safeParse(value).success, {
        error: messages.emailInvalid,
      }),
    password: z.string().min(1, { error: messages.passwordRequired }),
  })
}

export type SignInValues = z.output<ReturnType<typeof signInSchema>>
