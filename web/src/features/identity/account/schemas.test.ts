import { describe, expect, it } from 'vitest'
import { accountSchema, passwordChangeSchema } from './schemas'

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

describe('the current password (SCR-ACC-01, signed in again before the change)', () => {
  const withCurrent = (
    currentPassword: string,
    newPassword = 'mot mat khau moi',
    confirmation = newPassword,
  ) =>
    passwordChangeSchema({ ...passwordMessages, currentRequired: 'currentRequired' }).safeParse({
      currentPassword,
      newPassword,
      confirmation,
    })

  it('is required when the form asks for it', () => {
    expect(errorsOf(withCurrent(''))).toEqual({ currentPassword: 'currentRequired' })
  })

  it('is kept as typed, never trimmed', () => {
    expect(withCurrent(' mat khau cu ').data?.currentPassword).toBe(' mat khau cu ')
  })

  it('does not hide the other errors while it is empty', () => {
    expect(errorsOf(withCurrent('', 'mot mat khau moi', 'khac'))).toEqual({
      currentPassword: 'currentRequired',
      confirmation: 'mismatch',
    })
  })

  it('is not asked for on SCR-AUT-02', () => {
    expect(change('mot mat khau moi').success).toBe(true)
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
