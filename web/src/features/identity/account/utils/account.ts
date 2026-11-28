/** The muted line under the h1 of SCR-ACC-01: the name and email of GET /me, the name alone without an email. */
export function headline({ name, email }: { name: string; email?: string | null }): string {
  return [name, email].filter(Boolean).join(' · ')
}
