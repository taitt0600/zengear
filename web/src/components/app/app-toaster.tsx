'use client'

import { useTranslations } from 'next-intl'
import { Toaster } from '@/components/ui/sonner'

/**
 * The toasts of the admin part (docs/SCREENS.md, Conventions: "Đã lưu." and the problems no form field shows). The
 * name of their region comes from the message file: sonner's own is English (DEC-104 (6)).
 */
export function AppToaster() {
  const t = useTranslations('admin')
  return <Toaster containerAriaLabel={t('notifications')} />
}
