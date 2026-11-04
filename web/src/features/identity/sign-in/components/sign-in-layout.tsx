import type { ReactNode } from 'react'

/** The layout of SCR-AUT-01 (block login-03): the store name above one card, centred on the muted ground. */
export function SignInLayout({ brand, children }: { brand: ReactNode; children: ReactNode }) {
  return (
    <main className="flex min-h-svh flex-col items-center justify-center gap-6 bg-muted p-6 md:p-10">
      <div className="flex w-full max-w-sm flex-col gap-6">
        {brand}
        {children}
      </div>
    </main>
  )
}
