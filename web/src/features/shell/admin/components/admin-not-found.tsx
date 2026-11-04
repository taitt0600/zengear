import { SearchXIcon } from 'lucide-react'
import { useTranslations } from 'next-intl'
import { AdminPageStatus } from './admin-page-status'

/** The 404 of an admin URL, inside the frame and its guard (SCR-ADM-01), with a way back to the landing page. */
export function AdminNotFound() {
  const t = useTranslations('admin')
  return (
    <AdminPageStatus
      icon={<SearchXIcon aria-hidden />}
      title={t('notFound')}
      description={t('notFoundHint')}
      href="/admin"
      label={t('home')}
    />
  )
}
