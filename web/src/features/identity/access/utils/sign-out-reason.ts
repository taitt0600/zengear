// The text of a sign-out that the admin layout made for a problem of the API (403 account-disabled or
// account-not-registered), kept for the next page: the sign-in page shows it once (DEC-117 (4)). The text is the API's
// `detail`, never a value of the user. When the browser blocks its storage, the sign-in page shows no reason.
const KEY = 'zengear.signOutReason'

export function keepSignOutReason(text: string): void {
  try {
    window.sessionStorage.setItem(KEY, text)
  } catch {
    // Storage blocked: the sign-in page opens without the reason.
  }
}

/** The kept text, removed as it is read, so that it shows once; null when there is none. */
export function takeSignOutReason(): string | null {
  try {
    const text = window.sessionStorage.getItem(KEY)
    window.sessionStorage.removeItem(KEY)
    return text
  } catch {
    return null
  }
}
