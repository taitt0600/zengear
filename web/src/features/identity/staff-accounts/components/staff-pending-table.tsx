'use client'

import { CircleCheckIcon } from 'lucide-react'
import Link from 'next/link'
import { useTranslations } from 'next-intl'
import { useId, useRef, useState, type Ref } from 'react'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import type { StaffListItemDto } from '@/lib/api/generated/model'
import type { ApprovalAction } from '../hooks/use-staff-approval'
import { useDateTime } from '../hooks/use-staff-texts'
import { StaffListEmpty } from './staff-list-empty'
import { StaffPendingDialog } from './staff-pending-actions'
import { StaffPendingBlock } from './staff-pending-block'
import { StaffPendingRow } from './staff-pending-row'

/**
 * The tab Chờ duyệt of SCR-STF-01 (STF-09, R-USR-17, UC-140): a line on why the accounts wait, then the box of the
 * table with STT, Họ tên and the email, Vai trò, Người tạo, Ngày tạo and Thao tác, no sort; its one confirmation for
 * the action a row chose, sent with the item's `version` (`StaffPendingDialog`), after which the list and the
 * count reload, so the row leaves the tab. Below md the rows are blocks (`StaffPendingBlock`). Empty: "Không còn tài khoản chờ duyệt." with a link to the tab Tất cả.
 */
export function StaffPendingTable({
  rows,
  allHref,
  onGone,
  ref,
}: {
  rows: (StaffListItemDto & { number: number })[]
  allHref: string
  onGone: () => void
  ref?: Ref<HTMLDivElement>
}) {
  const t = useTranslations('staff')
  const dateTime = useDateTime()
  // The last choice stays while the dialog closes, so that Radix can still put the focus back.
  const [chosen, setChosen] = useState<{ action: ApprovalAction; row: StaffListItemDto } | null>(
    null,
  )
  const [open, setOpen] = useState(false)
  const opener = useRef<HTMLElement | null>(null)
  const descriptionId = useId()

  if (rows.length === 0) {
    return (
      // The target of the focus once the last row left with its dialog open.
      <div ref={ref} tabIndex={-1}>
        <StaffListEmpty
          icon={<CircleCheckIcon aria-hidden />}
          title={t('list.pendingEmpty.title')}
          description={t('list.pendingEmpty.description')}
          action={
            <Button asChild variant="outline">
              <Link href={allHref}>{t('list.pendingEmpty.showAll')}</Link>
            </Button>
          }
        />
      </div>
    )
  }

  const texts = {
    noValue: t('list.noValue'),
    approve: t('account.approve'),
    reject: t('account.reject'),
    withdraw: t('account.withdraw'),
    view: t('list.actions.view'),
    label: (action: ApprovalAction | 'view', name: string) =>
      action === 'view'
        ? t('list.actions.viewLabel', { name })
        : t(`list.actions.${action}Label`, { name }),
  }
  const choose = (action: ApprovalAction, row: StaffListItemDto) => {
    opener.current = document.activeElement as HTMLElement | null
    setChosen({ action, row })
    setOpen(true)
  }
  // The row has usually left the tab once the dialog closes: the focus goes to the table then.
  const focusBack = () => (opener.current?.isConnected ? opener.current.focus() : onGone())

  return (
    // The focus target once a row left with its dialog open: shown at every width, where the box of the table is
    // hidden below md.
    <div
      ref={ref}
      role="group"
      aria-labelledby={descriptionId}
      tabIndex={-1}
      className="flex flex-col gap-3"
    >
      <p id={descriptionId} className="text-muted-foreground text-sm">
        {t('list.pending.description')}
      </p>
      <div
        role="region"
        // eslint-disable-next-line jsx-a11y/no-noninteractive-tabindex
        tabIndex={0}
        aria-label={t('list.tableLabel')}
        className="hidden overflow-x-auto md:block [&>[data-slot=table-container]]:overflow-visible"
      >
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead className="text-right">
                <span aria-hidden>{t('list.columns.number')}</span>
                <span className="sr-only">{t('list.columns.numberFull')}</span>
              </TableHead>
              <TableHead>{t('list.columns.name')}</TableHead>
              <TableHead>{t('list.columns.roles')}</TableHead>
              <TableHead>{t('list.columns.createdBy')}</TableHead>
              <TableHead>{t('list.columns.createdAt')}</TableHead>
              <TableHead>{t('list.columns.actions')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map((row) => (
              <StaffPendingRow
                key={row.id}
                row={row}
                texts={texts}
                dateTime={dateTime}
                onAction={choose}
              />
            ))}
          </TableBody>
        </Table>
      </div>
      <ul aria-label={t('list.tableLabel')} className="flex flex-col gap-3 md:hidden">
        {rows.map((row) => (
          <StaffPendingBlock
            key={row.id}
            row={row}
            texts={texts}
            createdBy={
              row.createdBy && row.createdAt
                ? t('list.pending.createdBy', {
                    name: row.createdBy.name,
                    time: dateTime(row.createdAt),
                  })
                : undefined
            }
            onAction={choose}
          />
        ))}
      </ul>
      {chosen && (
        <StaffPendingDialog
          action={chosen.action}
          row={chosen.row}
          open={open}
          onOpenChange={setOpen}
          onClosed={focusBack}
        />
      )}
    </div>
  )
}
