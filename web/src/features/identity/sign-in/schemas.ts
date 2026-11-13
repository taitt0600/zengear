import { z } from 'zod'

// The input shape of SCR-AUT-01 (docs/screens/SCR-AUT-01.md). Firebase and the API decide the rest (UC-001).

/** The company email, trimmed, and the password exactly as typed: Firebase compares what is typed (DEC-102). */
export function signInSchema(messages: {
  emailRequired: string
  emailInvalid: string
  passwordRequired: string
}) {
  return z.object({
    email: z
      .string()
      .trim()
      .min(1, { error: messages.emailRequired })
      .pipe(z.email({ error: messages.emailInvalid })),
    password: z.string().min(1, { error: messages.passwordRequired }),
  })
}

export type SignInValues = z.output<ReturnType<typeof signInSchema>>
