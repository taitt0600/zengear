import { getRequestConfig } from 'next-intl/server'
import { defaultLocale } from './config'

// The locale of config.ts, without locale routing (DEC-104 (6), DEC-118 (4)); dates show in Vietnam time.
export default getRequestConfig(async () => ({
  locale: defaultLocale,
  timeZone: 'Asia/Ho_Chi_Minh',
  messages: (await import(`../../messages/${defaultLocale}.json`)).default,
}))
