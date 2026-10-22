import { describe, expect, it } from 'vitest'
import { accountSchema, passwordChangeSchema } from './account-schemas'

const passwordMessages = { required: 'required', length: 'length', mismatch: 'mismatch' }
const nameMessages = { required: 'required', tooLong: 'tooLong' }

/** The messages of a failed parse, by field. */
function errorsOf(result: {
  success: boolean
  error?: { issues: { path: PropertyKey[]; message: string }[] }
}) {
  return Object.fromEntries(
    (result.error?.issues ?? []).map((issue) => [issue.path.join('.'), issue.message]),
  )
}

const change = (newPassword: string, confirmation = newPassword) =>
  passwordChangeSchema(passwordMessages).safeParse({ newPassword, confirmation })

describe('the new password (SCR-AUT-02, the limits of R-USR-11 the web checks)', () => {
  it('is required', () => {
    expect(errorsOf(change(''))).toEqual({ newPassword: 'required' })
  })

  it.each([
    [11, false],
    [12, true],
    [128, true],
    [129, false],
  ])('of %i characters is accepted: %s', (length, accepted) => {
    const result = change('a'.repeat(length))
    expect(result.success).toBe(accepted)
    if (!accepted) expect(errorsOf(result)).toEqual({ newPassword: 'length' })
  })

  it('counts Unicode code points, not UTF-16 units', () => {
    // 11 letters and one emoji: 12 code points, 13 UTF-16 units.
    expect(change(`${'a'.repeat(11)}😀`).success).toBe(true)
    // 128 emoji: 128 code points, 256 UTF-16 units.
    expect(change('😀'.repeat(128)).success).toBe(true)
    expect(change(`${'a'.repeat(10)}😀`).success).toBe(false)
  })

  it('keeps and counts spaces, never trims', () => {
    const typed = '  mat khau dai  '
    const result = change(typed)
    expect(result.success).toBe(true)
    expect(result.data?.newPassword).toBe(typed)
    // Ten letters and two spaces: 12 characters with the spaces.
    expect(change(' abcdefghij ').success).toBe(true)
  })

  it('must be typed twice the same', () => {
    expect(errorsOf(change('mat-khau-moi-1', 'mat-khau-moi-2'))).toEqual({
      confirmation: 'mismatch',
    })
  })
})

const rename = (name: string) => accountSchema(nameMessages).safeParse({ name })

describe('the display name (SCR-ACC-01)', () => {
  it.each(['', '   '])('is required: %j', (name) => {
    expect(errorsOf(rename(name))).toEqual({ name: 'required' })
  })

  it('is trimmed, and counted after trimming', () => {
    const result = rename(`  ${'a'.repeat(100)}  `)
    expect(result.success).toBe(true)
    expect(result.data?.name).toBe('a'.repeat(100))
  })

  it('holds at most 100 characters', () => {
    expect(errorsOf(rename('a'.repeat(101)))).toEqual({ name: 'tooLong' })
  })

  it('counts UTF-16 units, as the API does', () => {
    // 51 emoji: 51 code points, 102 UTF-16 units.
    expect(errorsOf(rename('😀'.repeat(51)))).toEqual({ name: 'tooLong' })
    expect(rename('😀'.repeat(50)).success).toBe(true)
  })
})
