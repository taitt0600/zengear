import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'

/**
 * The frame while the session and GET /me load: the menu (brand, two entries, user), the header and a page title, never
 * an empty screen. Rendered before SidebarProvider, so its widths are the sidebar's own (16rem, 3rem collapsed).
 */
export function AdminShellSkeleton({ collapsed }: { collapsed: boolean }) {
  const item = (height: string) => (collapsed ? 'size-8' : `${height} w-full`)
  return (
    <div className="flex min-h-svh w-full">
      <div
        className={cn(
          'hidden shrink-0 flex-col gap-2 border-r bg-sidebar p-2 md:flex',
          collapsed ? 'w-12' : 'w-64',
        )}
      >
        <Skeleton className={item('h-12')} />
        <div className="flex flex-1 flex-col gap-1 py-2">
          <Skeleton className={item('h-8')} />
          <Skeleton className={item('h-8')} />
        </div>
        <Skeleton className={item('h-12')} />
      </div>
      <div className="flex flex-1 flex-col">
        {/* Collapsed, the header is lower on a wide screen only: the narrow sheet does not collapse. */}
        <div className={cn('flex h-16 shrink-0 items-center gap-2 px-4', collapsed && 'md:h-12')}>
          <Skeleton className="size-7" />
          <Skeleton className="h-4 w-40" />
        </div>
        <div className="flex flex-col gap-4 p-4 md:p-6">
          <Skeleton className="h-7 w-64 max-w-full" />
          <Skeleton className="h-5 w-96 max-w-full" />
        </div>
      </div>
    </div>
  )
}
