import { z } from 'zod'
import { hasPasswordLength } from './utils/password-rules'

// The input shapes of SCR-AUT-02 and SCR-ACC-01 (docs/screens/). The web checks only what these rules list; the API
// decides (R-USR-11 also refuses the default password, the blocklist and "zengear"; R-USR-04 the display name).

/** The display name after trimming, in UTF-16 units, as the API counts it (`Trim().Length`). */
const NAME_MAX_LENGTH = 100

/**
 * The new password, typed twice, and on SCR-ACC-01 the current password, which Firebase checks before the change
 * (`currentRequired`, its message, asks for it; SCR-AUT-02 leaves it empty and never shows it). Never trimmed: Firebase
 * compares exactly what is typed at sign-in (DEC-102).
 */
export function passwordChangeSchema(messages: {
  required: string
  length: string
  mismatch: string
  currentRequired?: string
}) {
  const current = z.string()
  return z
    .object({
      currentPassword: (messages.currentRequired
        ? current.min(1, { error: messages.currentRequired })
        : current
      ).default(''),
      newPassword: z.string().superRefine((value, context) => {
        if (value.length === 0) {
          context.addIssue({ code: 'custom', message: messages.required })
        } else if (!hasPasswordLength(value)) {
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
