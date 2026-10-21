import type messages from '../messages/vi.json'

// A message key that messages/vi.json does not hold fails the typecheck.
declare module 'next-intl' {
  interface AppConfig {
    Locale: 'vi'
    Messages: typeof messages
  }
}
