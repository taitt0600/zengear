/**
 * The empty div after the last card of SCR-ACC-01, which tells the section navigation (`useActiveSection` of `@/hooks`)
 * when the end of the page is in view. Here, not in the client section that passes it to the hook, because the layout
 * that renders it is a server component, and a value imported from a `'use client'` module reaches a server component
 * as a client reference, not as the string.
 */
export const SECTIONS_END_ID = 'account-end'
