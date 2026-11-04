import { StoreIcon } from 'lucide-react'
import { useTranslations } from 'next-intl'

/** The store name above the sign-in card: text, not a link, since a signed-out /admin comes back here. */
export function SignInBrand() {
  const t = useTranslations('app')
  return (
    <div className="flex items-center gap-2 self-center font-medium">
      <div className="flex size-6 items-center justify-center rounded-md bg-primary text-primary-foreground">
        <StoreIcon className="size-4" aria-hidden />
      </div>
      {t('name')}
    </div>
  )
}
