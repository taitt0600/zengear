import { CheckIcon, CircleIcon, InfoIcon } from 'lucide-react'
import { useTranslations } from 'next-intl'
import { cn } from '@/lib/utils'

export type PasswordRule = { id: string; text: string; state: 'met' | 'unmet' | 'neutral' }

const ICONS = { met: CheckIcon, unmet: CircleIcon, neutral: InfoIcon }

/**
 * Presentational: a checklist under a password field of SCR-ACC-01 (its Layout, Password card). A met item shows
 * CheckIcon in `foreground`, an unmet one CircleIcon in `muted-foreground`, so they differ in shape and lightness, not
 * hue, with a visually hidden "(đạt)" or "(chưa đạt)"; a neutral item, which the API checks on the send, says no state.
 * The field's `aria-describedby` lists the items' ids, so the list itself has no `aria-label`; `heading` names it for
 * a screen reader browsing the page. Nothing is announced while typing (no aria-live).
 */
export function PasswordRules({ rules, heading }: { rules: PasswordRule[]; heading?: string }) {
  const t = useTranslations('account.passwordRules')
  return (
    <div>
      {heading && <h3 className="sr-only">{heading}</h3>}
      <ul className="flex flex-col gap-1.5">
        {rules.map(({ id, text, state }) => {
          const Icon = ICONS[state]
          return (
            <li
              key={id}
              id={id}
              className={cn(
                'flex items-start gap-2 text-sm text-muted-foreground',
                state === 'met' && 'text-foreground',
              )}
            >
              <Icon aria-hidden className="mt-0.5 size-4 shrink-0" />
              <span>
                {text}
                {state !== 'neutral' && (
                  <>
                    {' '}
                    <span className="sr-only">{t(state)}</span>
                  </>
                )}
              </span>
            </li>
          )
        })}
      </ul>
    </div>
  )
}
