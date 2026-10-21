import { getTranslations } from 'next-intl/server'

/** A screen whose feature is not built yet: its title and one sentence, so that its menu entry never leads to a 404. */
export async function ScreenPlaceholder({ title }: { title: string }) {
  const t = await getTranslations('admin')
  return (
    <div className="flex flex-col gap-2">
      <h1 className="text-xl font-semibold">{title}</h1>
      <p className="text-sm text-muted-foreground">{t('placeholder')}</p>
    </div>
  )
}
