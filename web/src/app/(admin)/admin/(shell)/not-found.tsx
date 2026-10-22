import { getTranslations } from 'next-intl/server'
import Link from 'next/link'

/** The 404 of an admin URL, inside the layout and its guard (SCR-ADM-01), with a way back to the landing page. */
export default async function AdminNotFound() {
  const t = await getTranslations('admin')
  return (
    <div className="flex flex-col items-start gap-4">
      <h1 className="text-xl font-semibold">{t('notFound')}</h1>
      <Link
        href="/admin"
        className="inline-flex min-h-6 items-center text-sm font-medium underline underline-offset-4"
      >
        {t('home')}
      </Link>
    </div>
  )
}
