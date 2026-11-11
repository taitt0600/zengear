'use client'

import { ThemeProvider as NextThemesProvider } from 'next-themes'
import type { ReactNode } from 'react'

/**
 * The theme of the admin part (DEC-123 (2)): light, dark or the system's, the system's by default, kept in the
 * browser's localStorage under `zengear-theme` and set as the class of `<html>`. next-themes writes an inline script
 * that sets the class before the page paints; under the CSP of /admin it runs only with the request's nonce, which
 * next-themes puts on it in the server render.
 */
export function ThemeProvider({ nonce, children }: { nonce?: string; children: ReactNode }) {
  return (
    <NextThemesProvider
      attribute="class"
      defaultTheme="system"
      enableSystem
      disableTransitionOnChange
      storageKey="zengear-theme"
      nonce={nonce}
    >
      {children}
    </NextThemesProvider>
  )
}
