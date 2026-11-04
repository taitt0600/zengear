import { TriangleAlertIcon } from 'lucide-react'
import { useTranslations } from 'next-intl'
import { Button } from '@/components/ui/button'
import { Empty, EmptyContent, EmptyHeader, EmptyMedia, EmptyTitle } from '@/components/ui/empty'

/**
 * GET /me failed (or the sign-out did): the problem's text with "Thử lại" and "Đăng xuất", in a page of its own,
 * without the frame, which needs the user. Its title is the API's text, so it has no description (Help on screen).
 */
export function SessionError({
  text,
  onRetry,
  onSignOut,
}: {
  text: string
  onRetry: () => void
  onSignOut: () => void
}) {
  const t = useTranslations('admin')
  return (
    <main className="flex flex-1 items-center justify-center p-4">
      <Empty className="max-w-md flex-none border">
        <EmptyHeader>
          <EmptyMedia variant="icon">
            <TriangleAlertIcon aria-hidden />
          </EmptyMedia>
          <EmptyTitle role="alert" className="text-destructive">
            {text}
          </EmptyTitle>
        </EmptyHeader>
        <EmptyContent>
          <div className="flex gap-2">
            <Button onClick={onRetry}>{t('retry')}</Button>
            <Button variant="outline" onClick={onSignOut}>
              {t('user.signOut')}
            </Button>
          </div>
        </EmptyContent>
      </Empty>
    </main>
  )
}
