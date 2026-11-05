import { describe, expect, it } from 'vitest'
import { breadcrumbOf, firstEntry, visibleMenu } from './admin-menu'

describe('visibleMenu', () => {
  it('shows every entry to a user with every permission', () => {
    expect(visibleMenu(['business_location.update', 'staff.read'])).toEqual([
      { key: 'staff', href: '/admin/staff', permission: 'staff.read' },
      {
        key: 'settings',
        entries: [
          {
            key: 'businessLocation',
            href: '/admin/settings/business-location',
            permission: 'business_location.update',
          },
        ],
      },
    ])
  })

  it('hides an entry without its permission, and a group left empty', () => {
    expect(visibleMenu(['staff.read', 'brand.read'])).toEqual([
      { key: 'staff', href: '/admin/staff', permission: 'staff.read' },
    ])
  })

  it('is empty without any menu permission', () => {
    expect(visibleMenu([])).toEqual([])
  })
})

describe('breadcrumbOf', () => {
  const staff = { key: 'admin.menu.staff', href: '/admin/staff' }
  it.each([
    ['/admin/staff', [{ key: 'admin.menu.staff' }]],
    [
      '/admin/settings/business-location',
      [{ key: 'admin.menu.settings' }, { key: 'admin.menu.businessLocation' }],
    ],
    ['/admin/account', [{ key: 'admin.user.account' }]],
    ['/admin', []],
    // A page under an entry (SCR-STF-03): the entry as a link, then the page's title; any id, which the API checks.
    ['/admin/staff/0199a0f3-0000-7000-8000-000000000002', [staff, { key: 'staff.account.title' }]],
    ['/admin/staff/khong-ton-tai', [staff, { key: 'staff.account.title' }]],
    // SCR-STF-02 has no Layout yet: its placeholder page keeps the entry alone.
    ['/admin/staff/new', [{ key: 'admin.menu.staff' }]],
    // An unknown path, the 404: no breadcrumb, also under an entry.
    ['/admin/staff/a/b', []],
    ['/admin/khong-ton-tai', []],
  ])('%s', (pathname, crumbs) => {
    expect(breadcrumbOf(pathname)).toEqual(crumbs)
  })
})

describe('firstEntry', () => {
  it('is the first entry the user can open, inside a group too', () => {
    expect(firstEntry(['staff.read'])?.href).toBe('/admin/staff')
    expect(firstEntry(['business_location.update'])?.href).toBe('/admin/settings/business-location')
    expect(firstEntry([])).toBeUndefined()
  })

  it('skips the page the user is on', () => {
    const all = ['business_location.update', 'staff.read']
    expect(firstEntry(all, '/admin/staff')?.href).toBe('/admin/settings/business-location')
    expect(firstEntry(['staff.read'], '/admin/staff')).toBeUndefined()
  })
})
