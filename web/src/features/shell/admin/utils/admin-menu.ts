import { adminRoutes } from '@/features/identity/access'

// The menu of SCR-ADM-01 (docs/screens/SCR-ADM-01.md): each entry with the permission of the screen it opens. An entry
// the user's permissions do not allow is hidden (never by role name, R-SEC-03); the API still answers 403.
export type MenuEntry = { key: 'staff' | 'businessLocation'; href: string; permission: string }
export type MenuGroup = { key: 'settings'; entries: MenuEntry[] }
export type MenuItem = MenuEntry | MenuGroup

export const adminMenu: readonly MenuItem[] = [
  { key: 'staff', href: adminRoutes.staff, permission: 'staff.read' },
  {
    key: 'settings',
    entries: [
      {
        key: 'businessLocation',
        href: adminRoutes.businessLocation,
        permission: 'business_location.update',
      },
    ],
  },
]

/** The menu as the user may use it: entries without their permission, and groups left empty, are removed. */
export function visibleMenu(permissions: readonly string[]): MenuItem[] {
  return adminMenu.flatMap((item): MenuItem[] => {
    if (!('entries' in item)) return permissions.includes(item.permission) ? [item] : []
    const entries = item.entries.filter((entry) => permissions.includes(entry.permission))
    return entries.length ? [{ ...item, entries }] : []
  })
}

/** A step of the breadcrumb: a key of the message file, and the link of a step that is not the current page. */
export type Crumb = {
  key:
    | `admin.menu.${MenuEntry['key'] | MenuGroup['key']}`
    | 'admin.user.account'
    | 'staff.account.title'
  href?: string
}

// The pages under an entry, each with the title its Layout names (SCR-STF-03). SCR-STF-02 (`/admin/staff/new`) has
// no Layout yet: its placeholder keeps the entry alone.
const STAFF_ACCOUNT = new RegExp(`^${adminRoutes.staff}/(?!new$)[^/]+$`)

/**
 * Where the page sits, for the breadcrumb of the header: the entry's group as text, then the entry; a page under an
 * entry adds its title, the entry then a link; the account page alone. By the exact path, so that /admin and an
 * unknown path (the 404) have none.
 */
export function breadcrumbOf(pathname: string): Crumb[] {
  if (pathname === adminRoutes.account) return [{ key: 'admin.user.account' }]
  if (STAFF_ACCOUNT.test(pathname)) {
    return [{ key: 'admin.menu.staff', href: adminRoutes.staff }, { key: 'staff.account.title' }]
  }
  if (pathname === `${adminRoutes.staff}/new`) return [{ key: 'admin.menu.staff' }]
  for (const item of adminMenu) {
    if (!('entries' in item)) {
      if (item.href === pathname) return [{ key: `admin.menu.${item.key}` }]
      continue
    }
    const entry = item.entries.find((candidate) => candidate.href === pathname)
    if (entry) return [{ key: `admin.menu.${item.key}` }, { key: `admin.menu.${entry.key}` }]
  }
  return []
}

/**
 * The first menu entry the user can open, other than the page `except` they are on, for the link of the
 * no-permission page; undefined when there is none.
 */
export function firstEntry(permissions: readonly string[], except?: string): MenuEntry | undefined {
  return visibleMenu(permissions)
    .flatMap((item) => ('entries' in item ? item.entries : [item]))
    .find((entry) => entry.href !== except)
}
