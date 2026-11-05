const firstLetter = (word: string) => /\p{L}/u.exec(word)?.[0]

/**
 * The letters of an avatar (the user menu of SCR-ADM-01, the heading of SCR-STF-03): the first letters of the first and
 * last words of the name.
 */
export function initials(name: string): string {
  const letters = name.split(/\s+/).flatMap((word) => firstLetter(word) ?? [])
  if (letters.length === 0) return ''
  const last = letters.length > 1 ? letters[letters.length - 1] : ''
  return `${letters[0]}${last}`.toUpperCase()
}
