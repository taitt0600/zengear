import type { Metadata } from 'next'
import { getTranslations } from 'next-intl/server'
import { notFound } from 'next/navigation'

// The title of the 404 is the page's own: the server never renders not-found.tsx (see below), so Next.js never
// applies the metadata of that file.
export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('admin')
  return { title: t('notFound') }
}

/**
 * Any /admin URL that matches no page: the 404 of the admin layout, rendered at the request like every admin page, so
 * its scripts carry the nonce of the CSP (DEC-114 (2)) instead of the 404 that Next.js prerenders without one. The
 * layout renders its pages in the browser only, once the session is known (AdminShell), so the server never meets
 * this notFound(): the answer keeps the status 200 and the browser shows not-found.tsx.
 */
export default function MissingPage(): never {
  notFound()
}
