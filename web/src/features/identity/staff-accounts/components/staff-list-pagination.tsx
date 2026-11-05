'use client'

import { useTranslations } from 'next-intl'
import Link from 'next/link'
import {
  Pagination,
  PaginationContent,
  PaginationItem,
  PaginationNext,
  PaginationPrevious,
} from '@/components/ui/pagination'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'

/**
 * Presentational: the footer of the list of SCR-STF-01, the number of accounts and the links to the pages around
 * this one; a link that leads nowhere is given no `href`. It renders its props, but the texts of the page links,
 * which are the pagination's own words (`ui.pagination`), it reads itself, as components/ui/pagination.tsx does.
 */
export function StaffListPagination({
  total,
  position,
  previous,
  next,
}: {
  total: string
  position: string
  previous?: string
  next?: string
}) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-3 border-t pt-4">
      <span className="text-sm text-muted-foreground tabular-nums">{total}</span>
      <Pagination className="mx-0 w-auto">
        <PaginationContent>
          <PaginationItem>
            <PageLink direction="previous" href={previous} />
          </PaginationItem>
          <PaginationItem>
            <span className="px-2 text-sm tabular-nums">{position}</span>
          </PaginationItem>
          <PaginationItem>
            <PageLink direction="next" href={next} />
          </PaginationItem>
        </PaginationContent>
      </Pagination>
    </div>
  )
}

// On a narrow screen the link shows only its chevron (the pagination hides its text below `sm`): the tooltip gives
// the text there, and only there. A link that leads nowhere takes neither the pointer nor the focus: no tooltip.
function PageLink({ direction, href }: { direction: 'previous' | 'next'; href?: string }) {
  const t = useTranslations('ui.pagination')
  const Item = direction === 'previous' ? PaginationPrevious : PaginationNext
  if (!href) {
    return <Item role="link" aria-disabled="true" className="pointer-events-none opacity-50" />
  }
  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <Item asChild>
          <Link href={href} />
        </Item>
      </TooltipTrigger>
      <TooltipContent className="sm:hidden">{t(direction)}</TooltipContent>
    </Tooltip>
  )
}
