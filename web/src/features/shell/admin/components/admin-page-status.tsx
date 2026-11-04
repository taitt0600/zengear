import { useTranslations } from 'next-intl'
import Link from 'next/link'
import type { ReactNode } from 'react'
import { Button } from '@/components/ui/button'
import {
  Empty,
  EmptyContent,
  EmptyDescription,
  EmptyHeader,
  EmptyMedia,
  EmptyTitle,
} from '@/components/ui/empty'

/**
 * A page of the frame that has nothing to show (no permission, not found): an Empty with an outline filling the page
 * slot, after shadcn's 404 example, its title the page's heading and one way out (Grouping of docs/SCREENS.md).
 */
export function AdminPageStatus({
  icon,
  title,
  description,
  href,
  label,
  onClick,
}: {
  icon: ReactNode
  title: string
  description: string
  href: string
  label: string
  onClick?: () => void
}) {
  const t = useTranslations('admin')
  return (
    <Empty className="border">
      <EmptyHeader>
        <EmptyMedia variant="icon">{icon}</EmptyMedia>
        <EmptyTitle>
          <h1>{title}</h1>
        </EmptyTitle>
        <EmptyDescription>{description}</EmptyDescription>
      </EmptyHeader>
      <EmptyContent>
        <Button asChild>
          <Link href={href} onClick={onClick}>
            {label}
          </Link>
        </Button>
        <EmptyDescription>{t('needHelp')}</EmptyDescription>
      </EmptyContent>
    </Empty>
  )
}
