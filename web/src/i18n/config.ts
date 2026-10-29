// The languages of the web, in one place (DEC-118 (4)): Vietnamese only in Phase 1 (DEC-104 (6)). The storefront adds
// English later; the admin and the POS stay Vietnamese.
export const locales = ['vi'] as const

export type Locale = (typeof locales)[number]

export const defaultLocale: Locale = 'vi'
