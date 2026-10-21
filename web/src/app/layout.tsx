import type { Metadata } from 'next'
import { NextIntlClientProvider } from 'next-intl'
import { getTranslations } from 'next-intl/server'
// Checks the NEXT_PUBLIC_* variables: every page renders in this layout, so next build fails on a bad value.
import '@/env'
import './globals.css'

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('app')
  return { title: { template: t('titleTemplate'), default: t('name') } }
}

export default function RootLayout({ children }: LayoutProps<'/'>) {
  return (
    <html lang="vi" className="h-full antialiased">
      <body className="flex min-h-full flex-col">
        <NextIntlClientProvider>{children}</NextIntlClientProvider>
      </body>
    </html>
  )
}
