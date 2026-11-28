import { useTranslations } from 'next-intl'
import { useId } from 'react'

/**
 * Presentational: the read-only rows of the card "Thông tin tài khoản" of SCR-ACC-01 (its Layout, Profile card), a
 * divided `dl` whose label column is 136px in a wide card and 112px in a narrow one (the card's container), the value
 * wrapping, never cut short, and named by its label; an empty value "—", as the rows of gap 11 show theirs. The Email
 * alone: gap 11 adds Mã nhân viên, Vai trò, SĐT công việc and Ngày vào làm once GET /me returns them.
 */
export function AccountDetails({ email }: { email: string | null | undefined }) {
  const t = useTranslations('account')
  return (
    <dl className="divide-y border-t">
      <DetailRow label={t('email')}>{email ?? t('details.noValue')}</DetailRow>
    </dl>
  )
}

function DetailRow({ label, children }: { label: string; children: string }) {
  const labelId = useId()
  return (
    <div className="grid grid-cols-[112px_minmax(0,1fr)] gap-x-3 py-3 @sm:grid-cols-[136px_minmax(0,1fr)]">
      <dt id={labelId} className="text-sm text-muted-foreground">
        {label}
      </dt>
      <dd aria-labelledby={labelId} className="min-w-0 text-sm wrap-anywhere">
        {children}
      </dd>
    </div>
  )
}
