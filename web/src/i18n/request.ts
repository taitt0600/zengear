import { getRequestConfig } from 'next-intl/server'

// Vietnamese only in Phase 1, without locale routing (DEC-104 (6)); dates show in Vietnam time.
export default getRequestConfig(async () => ({
  locale: 'vi',
  timeZone: 'Asia/Ho_Chi_Minh',
  messages: (await import('../../messages/vi.json')).default,
}))
