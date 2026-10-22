/**
 * The account a password form belongs to, for password managers: a username field the user does not see, as
 * Chromium's guidance on password forms asks for a form that shows no username ("Use hidden fields for implicit
 * information", https://www.chromium.org/developers/design-documents/create-amazing-password-forms/).
 */
export function AccountName({ email }: { email: string }) {
  return (
    <input type="email" name="username" autoComplete="username" value={email} readOnly hidden />
  )
}
