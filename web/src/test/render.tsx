import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import { NextIntlClientProvider } from 'next-intl'
import type { ReactNode } from 'react'
import { TooltipProvider } from '@/components/ui/tooltip'
import { defaultLocale } from '@/i18n/config'
import messages from '../../messages/vi.json'

/**
 * Renders `ui` with the providers of the app: the messages and time zone of next-intl, the tooltip delays of the admin
 * part, and a query client that does not retry, so that a failed call shows at once. Returns the client, and a
 * rerender that keeps the same providers.
 */
export function renderWithProviders(ui: ReactNode) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const wrap = (node: ReactNode) => (
    <NextIntlClientProvider locale={defaultLocale} messages={messages} timeZone="Asia/Ho_Chi_Minh">
      <QueryClientProvider client={client}>
        {/* The delays of app/(admin)/admin/layout.tsx, copied on purpose so that tests open tooltips as users do. */}
        <TooltipProvider delayDuration={700} skipDelayDuration={300}>
          {node}
        </TooltipProvider>
      </QueryClientProvider>
    </NextIntlClientProvider>
  )
  const result = render(wrap(ui))
  return { client, rerender: (node: ReactNode) => result.rerender(wrap(node)) }
}
