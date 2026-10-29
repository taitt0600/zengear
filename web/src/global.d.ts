import type messages from '../messages/vi.json'
import type { Locale as AppLocale } from './i18n/config'

// A message key that messages/vi.json does not hold fails the typecheck.
declare module 'next-intl' {
  interface AppConfig {
    Locale: AppLocale
    Messages: typeof messages
  }
}
