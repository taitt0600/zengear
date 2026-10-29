import { ChevronDownIcon, LogOutIcon, UserIcon } from 'lucide-react'
import { useTranslations } from 'next-intl'
import Link from 'next/link'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { adminRoutes } from '@/features/identity/access'

/** The signed-in user's name in the header, with "Tài khoản của tôi" and "Đăng xuất" (SCR-ADM-01). */
export function UserMenu({ name, onSignOut }: { name: string; onSignOut: () => void }) {
  const t = useTranslations('admin.user')
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" className="max-w-56">
          <span className="min-w-0 truncate" title={name}>
            {name}
          </span>
          <ChevronDownIcon data-icon="inline-end" aria-hidden />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuGroup>
          <DropdownMenuItem asChild>
            <Link href={adminRoutes.account}>
              <UserIcon aria-hidden />
              {t('account')}
            </Link>
          </DropdownMenuItem>
          <DropdownMenuItem onSelect={onSignOut}>
            <LogOutIcon aria-hidden />
            {t('signOut')}
          </DropdownMenuItem>
        </DropdownMenuGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
