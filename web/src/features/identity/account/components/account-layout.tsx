import type { ReactNode } from 'react'
import { Separator } from '@/components/ui/separator'

/** The layout of SCR-ACC-01 My account (DEC-118 (3)): where its sections sit, and nothing else. */
export function AccountLayout({
  title,
  profile,
  password,
}: {
  title: ReactNode
  profile: ReactNode
  password: ReactNode
}) {
  return (
    <div className="flex max-w-lg flex-col gap-6">
      <h1 className="text-xl font-semibold">{title}</h1>
      {profile}
      <Separator />
      {password}
    </div>
  )
}
