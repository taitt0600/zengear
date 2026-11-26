import { adminRoutes } from '@/features/identity/access'

// The menu of SCR-ADM-01 (docs/screens/SCR-ADM-01.md): each entry with the permissions of the screen it opens, all
// required. An entry the user's permissions do not allow is hidden (never by role name, R-SEC-03), and a group left
// empty; the API still answers 403. Every area of more than one page is a group with a submenu (Layout of 2026-11-14).
export type MenuEntry = {
  key: 'staffList' | 'staffNew' | 'staffPending' | 'businessLocation'
  href: string
  permissions: readonly string[]
}
export type MenuGroup = { key: 'staff' | 'settings'; entries: MenuEntry[] }

/** The tab Chờ duyệt of SCR-STF-01 (STF-09), an entry of its own for a holder of `staff.approve`. */
const PENDING_HREF = `${adminRoutes.staff}?status=PENDING`
const NEW_STAFF = `${adminRoutes.staff}/new`

const adminMenu: readonly MenuGroup[] = [
  {
    key: 'staff',
    entries: [
      { key: 'staffList', href: adminRoutes.staff, permissions: ['staff.read'] },
      { key: 'staffNew', href: NEW_STAFF, permissions: ['staff.create'] },
      { key: 'staffPending', href: PENDING_HREF, permissions: ['staff.read', 'staff.approve'] },
    ],
  },
  {
    key: 'settings',
    entries: [
      {
        key: 'businessLocation',
        href: adminRoutes.businessLocation,
        permissions: ['business_location.update'],
      },
    ],
  },
]

const allowed = (permissions: readonly string[]) => (entry: MenuEntry) =>
  entry.permissions.every((key) => permissions.includes(key))

/** The menu as the user may use it: entries without their permissions, and groups left empty, are removed. */
export function visibleMenu(permissions: readonly string[]): MenuGroup[] {
  return adminMenu.flatMap((group) => {
    const entries = group.entries.filter(allowed(permissions))
    return entries.length ? [{ ...group, entries }] : []
  })
}

const STAFF_ACCOUNT = new RegExp(`^${adminRoutes.staff}/(?!new$)[^/]+$`)

/**
 * The entry whose page is open, among those the user may use: "Chờ duyệt" on the tab Chờ duyệt (the query string
 * counts), "Danh sách nhân viên" on SCR-STF-01 and on SCR-STF-03 (also on the tab Chờ duyệt for a user without
 * `staff.approve`, who has no "Chờ duyệt"), "Thêm nhân viên" on SCR-STF-02; undefined elsewhere.
 */
export function currentEntry(
  pathname: string,
  status: string | null,
  permissions: readonly string[],
): MenuEntry | undefined {
  const entries = visibleMenu(permissions).flatMap((group) => group.entries)
  const find = (key: MenuEntry['key']) => entries.find((entry) => entry.key === key)
  if (pathname === NEW_STAFF) return find('staffNew')
  if (pathname === adminRoutes.staff && status === 'PENDING' && find('staffPending'))
    return find('staffPending')
  if (pathname === adminRoutes.staff || STAFF_ACCOUNT.test(pathname)) return find('staffList')
  return entries.find((entry) => entry.href === pathname)
}

/** A step of the breadcrumb: a key of the message file, and the link of a step that is not the current page. */
export type Crumb = {
  key:
    | `admin.menu.${MenuEntry['key'] | MenuGroup['key']}`
    | 'admin.user.account'
    | 'staff.account.title'
  href?: string
}

/**
 * Where the page sits, for the breadcrumb of the header: the entry's group as text, then the entry; a page under an
 * entry (SCR-STF-03) adds its title, the entry then a link; the account page alone. /admin and an unknown path (the
 * 404) have none. Built from the static menu, so that a page the user lacks the permission of keeps its place.
 */
export function breadcrumbOf(
  pathname: string,
  status: string | null,
  permissions: readonly string[],
): Crumb[] {
  if (pathname === adminRoutes.account) return [{ key: 'admin.user.account' }]
  if (STAFF_ACCOUNT.test(pathname)) {
    return [
      { key: 'admin.menu.staff' },
      { key: 'admin.menu.staffList', href: adminRoutes.staff },
      { key: 'staff.account.title' },
    ]
  }
  if (pathname === adminRoutes.staff) {
    const pending = status === 'PENDING' && permissions.includes('staff.approve')
    return [
      { key: 'admin.menu.staff' },
      { key: pending ? 'admin.menu.staffPending' : 'admin.menu.staffList' },
    ]
  }
  for (const group of adminMenu) {
    const entry = group.entries.find((candidate) => candidate.href === pathname)
    if (entry) return [{ key: `admin.menu.${group.key}` }, { key: `admin.menu.${entry.key}` }]
  }
  return []
}

/**
 * The first menu entry the user can open, other than the page `except` they are on, for the link of the
 * no-permission page; undefined when there is none.
 */
export function firstEntry(permissions: readonly string[], except?: string): MenuEntry | undefined {
  return visibleMenu(permissions)
    .flatMap((group) => group.entries)
    .find((entry) => entry.href !== except)
}
