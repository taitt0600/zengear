import type { Metadata } from 'next'
import { NextIntlClientProvider } from 'next-intl'
import { getLocale, getTranslations } from 'next-intl/server'
// Checks the NEXT_PUBLIC_* variables: every page renders in this layout, so next build fails on a bad value.
import '@/env'
import './globals.css'

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('app')
  return { title: { template: t('titleTemplate'), default: t('name') } }
}

export default async function RootLayout({ children }: LayoutProps<'/'>) {
  // Read before the JSX: jsx-a11y/html-has-lang cannot judge an await written in the attribute.
  const locale = await getLocale()
  return (
    // The theme's script sets the class of <html> before React hydrates it (DEC-123 (2)).
    <html lang={locale} className="h-full antialiased" suppressHydrationWarning>
      <body className="flex min-h-full flex-col">
        <NextIntlClientProvider>{children}</NextIntlClientProvider>
      </body>
    </html>
  )
}
