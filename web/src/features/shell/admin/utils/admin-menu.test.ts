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
  it.each([
    ['/admin/staff', ['menu.staff']],
    ['/admin/settings/business-location', ['menu.settings', 'menu.businessLocation']],
    ['/admin/account', ['user.account']],
    ['/admin', []],
    // An unknown path, the 404: no breadcrumb, also under an entry.
    ['/admin/staff/khong-ton-tai', []],
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
