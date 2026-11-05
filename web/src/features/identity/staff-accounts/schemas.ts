import { z } from 'zod'

/**
 * The dialog "Khóa tài khoản" of SCR-STF-03: whether the person left, and their last working day (`yyyy-MM-dd`). The
 * shape only: the API decides whether the day is allowed (R-USR-07). "Left without a day" has no text of its own,
 * since the dialog cannot reach it (ticking picks today, and the calendar never empties its day): a guard on the type.
 */
export const staffDisableSchema = z
  .object({ leaving: z.boolean(), leftDate: z.string().nullable() })
  .refine((value) => !value.leaving || value.leftDate !== null, { path: ['leftDate'] })

export type StaffDisableValues = z.infer<typeof staffDisableSchema>
