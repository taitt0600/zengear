import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import { NextIntlClientProvider } from 'next-intl'
import type { ReactNode } from 'react'
import { defaultLocale } from '@/i18n/config'
import messages from '../../messages/vi.json'

/**
 * Renders `ui` with the providers of the app: the messages and time zone of next-intl, and a query client that does
 * not retry, so that a failed call shows at once. Returns the client.
 */
export function renderWithProviders(ui: ReactNode): QueryClient {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <NextIntlClientProvider locale={defaultLocale} messages={messages} timeZone="Asia/Ho_Chi_Minh">
      <QueryClientProvider client={client}>{ui}</QueryClientProvider>
    </NextIntlClientProvider>,
  )
  return client
}
