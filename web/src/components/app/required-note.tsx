'use client'

import { useTranslations } from 'next-intl'

// Required fields (docs/screens/conventions.md): the note a form shows once under its description, and the mark at the
// end of a required field's label, the same on every admin screen (SCR-STF-03, SCR-ACC-01), so their texts are those
// of the namespace `admin`.

/** "Trường có dấu * là bắt buộc.", its `*` hidden from screen readers, which read `admin.requiredMark` in its place. */
export function RequiredNote() {
  const t = useTranslations('admin')
  return (
    <p className="text-sm text-muted-foreground">
      {t.rich('requiredNote', {
        mark: (star) => (
          <>
            <span aria-hidden>{star}</span>
            <span className="sr-only">{t('requiredMark')}</span>
          </>
        ),
      })}
    </p>
  )
}

/**
 * The ` *` of a required field's label (its space a margin, the `*` a text of the message file), in the label's own
 * colour; its input says `required` itself.
 */
export function RequiredMark() {
  const t = useTranslations('admin')
  return (
    <span aria-hidden className="ms-1">
      {t('requiredStar')}
    </span>
  )
}
