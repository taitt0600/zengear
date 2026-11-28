import { describe, expect, it } from 'vitest'
import { passwordRuleStates } from './password-rules'

// The checklist of R-USR-11 under the new password of SCR-ACC-01 (its Layout, Password card): what the web can check
// as the user types, counted as the API counts. The default password and the blocklist are the API's alone.

describe('the checklist of the new password', () => {
  it('shows every item unmet while nothing is typed', () => {
    expect(passwordRuleStates('', '')).toEqual({
      length: 'unmet',
      noZengear: 'unmet',
      match: 'unmet',
    })
  })

  it.each([
    [11, 'unmet'],
    [12, 'met'],
    [128, 'met'],
    [129, 'unmet'],
  ])('a password of %i characters: length %s', (length, state) => {
    expect(passwordRuleStates('a'.repeat(length), '').length).toBe(state)
  })

  it('counts Unicode code points, not UTF-16 units', () => {
    // 11 letters and one emoji: 12 code points, 13 UTF-16 units.
    expect(passwordRuleStates(`${'a'.repeat(11)}😀`, '').length).toBe('met')
    expect(passwordRuleStates('😀'.repeat(128), '').length).toBe('met')
  })

  it.each(['zengear', 'ZenGear', 'cuahangZENGEAR2026'])('"%s" contains zengear', (password) => {
    expect(passwordRuleStates(password, '').noZengear).toBe('unmet')
  })

  it('meets "no zengear" once a password without it is typed', () => {
    expect(passwordRuleStates('zen gear cua hang', '').noZengear).toBe('met')
  })

  it('meets the match once the repeat is typed and the same', () => {
    expect(passwordRuleStates('mat khau moi dai', 'mat khau').match).toBe('unmet')
    expect(passwordRuleStates('mat khau moi dai', 'mat khau moi dai').match).toBe('met')
  })
})
