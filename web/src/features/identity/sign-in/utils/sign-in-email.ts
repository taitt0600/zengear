/**
 * The email SCR-AUT-01 signs in to Firebase with (DEC-145 (4)): a value with `@` is the email as typed; a value without
 * one is a username, whose company email is, by R-USR-24, the username in lower case, `@` and the company domain.
 * No endpoint looks a username up, so the page tells nobody whether an account exists.
 */
export function signInEmail(identifier: string, domain: string): string {
  return identifier.includes('@') ? identifier : `${identifier}@${domain}`.toLowerCase()
}
