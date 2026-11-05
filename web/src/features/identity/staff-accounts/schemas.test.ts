import { describe, expect, it } from 'vitest'
import { staffDisableSchema } from './schemas'

// The shape of the dialog "Khóa tài khoản" (SCR-STF-03) before it sends; the API decides the dates (R-USR-07).
describe('staffDisableSchema', () => {
  it('takes an account locked without a left date', () => {
    expect(staffDisableSchema.safeParse({ leaving: false, leftDate: null }).success).toBe(true)
  })

  it('takes a person who left with their last working day', () => {
    expect(staffDisableSchema.safeParse({ leaving: true, leftDate: '2026-11-05' }).success).toBe(
      true,
    )
  })

  it('refuses a person who left without a day, which the dialog cannot reach', () => {
    const result = staffDisableSchema.safeParse({ leaving: true, leftDate: null })
    expect(result.success).toBe(false)
    expect(result.error?.issues[0].path).toEqual(['leftDate'])
  })
})
