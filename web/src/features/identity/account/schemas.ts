import { z } from 'zod'

// The input shapes of SCR-AUT-02 and SCR-ACC-01 (docs/SCREENS.md). The web checks only what these rules list; the API
// decides (R-USR-11 also refuses the default password, the blocklist and "zengear"; R-USR-04 the display name).

/** R-USR-11: in Unicode code points, as the API counts them. */
export const PASSWORD_MIN_LENGTH = 12
export const PASSWORD_MAX_LENGTH = 128
/** The display name after trimming, in UTF-16 units, as the API counts it (`Trim().Length`). */
export const NAME_MAX_LENGTH = 100

const codePoints = (value: string) => [...value].length

/** The new password, typed twice. Never trimmed: Firebase compares exactly what is typed at sign-in (DEC-102). */
export function passwordChangeSchema(messages: {
  required: string
  length: string
  mismatch: string
}) {
  return z
    .object({
      newPassword: z.string().superRefine((value, context) => {
        if (value.length === 0) {
          context.addIssue({ code: 'custom', message: messages.required })
        } else if (
          codePoints(value) < PASSWORD_MIN_LENGTH ||
          codePoints(value) > PASSWORD_MAX_LENGTH
        ) {
          context.addIssue({ code: 'custom', message: messages.length })
        }
      }),
      // Checked by the web only and never sent.
      confirmation: z.string(),
    })
    .refine((values) => values.confirmation === values.newPassword, {
      error: messages.mismatch,
      path: ['confirmation'],
    })
}

/** The display name, trimmed. */
export function accountSchema(messages: { required: string; tooLong: string }) {
  return z.object({
    name: z
      .string()
      .trim()
      .min(1, { error: messages.required })
      // Not .max(): Zod counts code points there, while the API counts UTF-16 units.
      .refine((name) => name.length <= NAME_MAX_LENGTH, { error: messages.tooLong }),
  })
}
