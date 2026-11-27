/**
 * The empty div after the last card of SCR-STF-03, which tells when the end of the page is in view. Here, not in the
 * client hook that watches it, because the layout that renders it is a server component, and a value imported from a
 * `'use client'` module reaches a server component as a client reference, not as the string.
 */
export const SECTIONS_END_ID = 'staff-account-end'
