import type { ReactNode } from 'react'
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'

/**
 * The layout of SCR-AUT-02 Required password change (DEC-118 (3)): a card alone in the page, without the menu, with
 * its content and its actions.
 */
export function ChangePasswordLayout({
  title,
  children,
  actions,
}: {
  title: ReactNode
  children: ReactNode
  actions: ReactNode
}) {
  return (
    <main className="flex flex-1 items-center justify-center p-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>
            <h1 className="text-xl">{title}</h1>
          </CardTitle>
        </CardHeader>
        <CardContent>{children}</CardContent>
        <CardFooter className="justify-end gap-2">{actions}</CardFooter>
      </Card>
    </main>
  )
}
