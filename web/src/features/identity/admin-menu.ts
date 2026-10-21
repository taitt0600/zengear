import { adminRoutes } from './routes'

// The menu of SCR-ADM-01 (docs/SCREENS.md): each entry with the permission of the screen it opens. An entry the
// user's permissions do not allow is hidden (never by role name, R-SEC-03); the API still answers 403.
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

/**
 * The first menu entry the user can open, other than the page `except` they are on, for the link of the
 * no-permission page; undefined when there is none.
 */
export function firstEntry(permissions: readonly string[], except?: string): MenuEntry | undefined {
  return visibleMenu(permissions)
    .flatMap((item) => ('entries' in item ? item.entries : [item]))
    .find((entry) => entry.href !== except)
}
