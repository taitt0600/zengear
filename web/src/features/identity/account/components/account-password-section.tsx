'use client'

import { useIsMutating, useQuery } from '@tanstack/react-query'
import { useTranslations } from 'next-intl'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { meQuery, PASSWORD_CHANGED, useStaffSignOut } from '@/features/identity/access'
import { changePasswordKey } from '../api/me'
import { ChangePasswordForm } from './change-password-form'

/**
 * "Đổi mật khẩu" of SCR-ACC-01: the form of SCR-AUT-02 in a dialog; after the change the user is signed out
 * (DEC-094). It stays open while the change is sent, so that its answer is shown.
 */
export function AccountPasswordSection() {
  const t = useTranslations()
  const signOut = useStaffSignOut()
  const email = useQuery(meQuery).data?.email
  const [open, setOpen] = useState(false)
  const changing = useIsMutating({ mutationKey: changePasswordKey }) > 0
  return (
    <Dialog open={open} onOpenChange={(next) => setOpen(next || changing)}>
      <DialogTrigger asChild>
        <Button variant="outline" className="self-start">
          {t('account.changePassword')}
        </Button>
      </DialogTrigger>
      {/* The form's own "Hủy" closes the dialog, so the corner close button is off. Taller
          than a short screen, the dialog scrolls inside instead of losing its top and its buttons. */}
      <DialogContent
        showCloseButton={false}
        className="max-h-[calc(100dvh-2rem)] overflow-y-auto overscroll-contain"
      >
        <DialogHeader>
          <DialogTitle>{t('changePassword.title')}</DialogTitle>
          <DialogDescription>{t('account.changePasswordNote')}</DialogDescription>
        </DialogHeader>
        <ChangePasswordForm
          email={email}
          onCancel={() => setOpen(false)}
          onChanged={() => signOut(PASSWORD_CHANGED)}
        />
      </DialogContent>
    </Dialog>
  )
}
