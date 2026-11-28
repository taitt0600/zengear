// The web's copy of the limits of R-USR-11 (DEC-102) that it can check as the user types; the API decides, and alone
// checks the default password and the blocklist.

/** R-USR-11: in Unicode code points, as the API counts them. */
const PASSWORD_MIN_LENGTH = 12
const PASSWORD_MAX_LENGTH = 128

const codePoints = (value: string) => [...value].length

/** Whether a password's length is within R-USR-11, in code points. */
export function hasPasswordLength(value: string): boolean {
  const length = codePoints(value)
  return length >= PASSWORD_MIN_LENGTH && length <= PASSWORD_MAX_LENGTH
}

export type PasswordRuleState = 'met' | 'unmet'

const BRAND = /zengear/i

/**
 * The checklist under the new password of SCR-ACC-01 (its Layout, Password card): the length, no "zengear" in any case,
 * and the repeat the same; each unmet while its field is empty, so that a blank form shows no check.
 */
export function passwordRuleStates(newPassword: string, confirmation: string) {
  const state = (met: boolean): PasswordRuleState => (met ? 'met' : 'unmet')
  return {
    length: state(hasPasswordLength(newPassword)),
    noZengear: state(newPassword.length > 0 && !BRAND.test(newPassword)),
    match: state(confirmation.length > 0 && confirmation === newPassword),
  }
}
