'use client'

import { EyeIcon, LockIcon, MoreHorizontalIcon, RotateCcwIcon } from 'lucide-react'
import { useTranslations } from 'next-intl'
import Link from 'next/link'
import { useRef, useState } from 'react'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import type { StaffListItemDto } from '@/lib/api/generated/model'
import { staffPath } from '../utils/paths'
import { StaffDisableDialog } from './staff-disable-dialog'
import { StaffRestoreDialog } from './staff-restore-dialog'

type Command = 'disable' | 'restore'

/**
 * The menu "Thao tác" of a row of SCR-STF-01 (STF-05, STF-02): "Xem chi tiết", and, when `canAct` (the user holds
 * `staff.disable` and the row is not their own account, R-USR-12), the commands of SCR-STF-03 for its status: lock an
 * active account, restore or lock again a disabled one (R-USR-03). They open the dialogs of SCR-STF-03, whose requests
 * load the list again (hooks/use-staff-status.ts). Hiding only: the API decides. A dialog that closes puts the focus
 * back on the menu button; when the row leaves the list with its dialog open (a 200 under a status filter), `onGone`
 * takes the focus instead.
 */
export function StaffRowActions({
  row,
  canAct,
  onGone,
}: {
  row: StaffListItemDto
  canAct: boolean
  onGone: () => void
}) {
  const t = useTranslations('staff')
  const [open, setOpen] = useState<Command | null>(null)
  const button = useRef<HTMLButtonElement>(null)
  // The command an item chose, opened once the menu has closed, so that the layers of the menu and the dialog never
  // overlap.
  const chosen = useRef<Command | null>(null)
  // Radix calls it once the dialog has closed, also when the row (and the menu button with it) has left the list.
  const focusBack = () => (button.current ? button.current.focus() : onGone())
  const choose = (command: Command) => {
    chosen.current = command
  }
  const toggle = (command: Command) => (next: boolean) => setOpen(next ? command : null)

  return (
    <>
      <DropdownMenu>
        <Tooltip>
          <TooltipTrigger asChild>
            <DropdownMenuTrigger asChild>
              <Button
                ref={button}
                variant="ghost"
                size="icon"
                aria-label={t('list.actions.label', { name: row.name })}
              >
                <MoreHorizontalIcon aria-hidden />
              </Button>
            </DropdownMenuTrigger>
          </TooltipTrigger>
          <TooltipContent>{t('list.columns.actions')}</TooltipContent>
        </Tooltip>
        <DropdownMenuContent
          align="end"
          onCloseAutoFocus={() => {
            if (chosen.current) setOpen(chosen.current)
            chosen.current = null
          }}
        >
          <DropdownMenuItem asChild>
            <Link href={staffPath(row.id)}>
              <EyeIcon aria-hidden />
              {t('list.actions.view')}
            </Link>
          </DropdownMenuItem>
          {canAct && row.status === 'ACTIVE' && (
            <>
              <DropdownMenuSeparator />
              <DropdownMenuItem variant="destructive" onSelect={() => choose('disable')}>
                <LockIcon aria-hidden />
                {t('account.disable')}
              </DropdownMenuItem>
            </>
          )}
          {canAct && row.status === 'DISABLED' && (
            <>
              <DropdownMenuSeparator />
              <DropdownMenuItem onSelect={() => choose('restore')}>
                <RotateCcwIcon aria-hidden />
                {t('account.restore')}
              </DropdownMenuItem>
              <DropdownMenuItem variant="destructive" onSelect={() => choose('disable')}>
                <LockIcon aria-hidden />
                {t('account.disableAgain')}
              </DropdownMenuItem>
            </>
          )}
        </DropdownMenuContent>
      </DropdownMenu>
      {canAct && (
        <>
          <StaffDisableDialog
            account={row}
            open={open === 'disable'}
            onOpenChange={toggle('disable')}
            onClosed={focusBack}
          />
          <StaffRestoreDialog
            account={row}
            open={open === 'restore'}
            onOpenChange={toggle('restore')}
            onClosed={focusBack}
          />
        </>
      )}
    </>
  )
}
