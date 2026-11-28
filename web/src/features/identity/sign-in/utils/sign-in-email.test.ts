import { describe, expect, it } from 'vitest'
import { signInEmail } from './sign-in-email'

describe('signInEmail', () => {
  it('turns a username into its company email, in lower case (R-USR-24)', () => {
    expect(signInEmail('HaLTZG260002', 'zengear.test')).toBe('haltzg260002@zengear.test')
  })

  it('uses the domain it is given, in lower case', () => {
    expect(signInEmail('AnNVZG260004', 'ZenGear.VN')).toBe('annvzg260004@zengear.vn')
  })

  it('keeps an email as typed', () => {
    expect(signInEmail('HaLTZG260002@ZenGear.test', 'other.example')).toBe(
      'HaLTZG260002@ZenGear.test',
    )
  })
})
