import { describe, expect, it } from 'vitest'
import { breadcrumbOf, currentEntry, firstEntry, visibleMenu } from './admin-menu'

const keys = (permissions: string[]) =>
  visibleMenu(permissions).map((group) => [group.key, group.entries.map((entry) => entry.key)])

describe('visibleMenu', () => {
  it('shows every entry to a user with every permission', () => {
    expect(
      keys(['business_location.update', 'staff.read', 'staff.create', 'staff.approve']),
    ).toEqual([
      ['staff', ['staffList', 'staffNew', 'staffPending']],
      ['settings', ['businessLocation']],
    ])
  })

  it('shows "Chờ duyệt" only with staff.read and staff.approve (STF-09)', () => {
    expect(keys(['staff.read'])).toEqual([['staff', ['staffList']]])
    expect(keys(['staff.approve'])).toEqual([])
    expect(keys(['staff.read', 'staff.approve'])).toEqual([
      ['staff', ['staffList', 'staffPending']],
    ])
  })

  it('shows "Thêm nhân viên" alone to a user with staff.create without staff.read', () => {
    expect(keys(['staff.create'])).toEqual([['staff', ['staffNew']]])
  })

  it('hides an entry without its permission, and a group left empty', () => {
    expect(keys(['staff.read', 'brand.read'])).toEqual([['staff', ['staffList']]])
    expect(visibleMenu([])).toEqual([])
  })
})

describe('currentEntry', () => {
  const approver = ['staff.read', 'staff.create', 'staff.approve']
  it.each([
    ['/admin/staff', null, approver, 'staffList'],
    ['/admin/staff', 'PENDING', approver, 'staffPending'],
    ['/admin/staff', 'ACTIVE', approver, 'staffList'],
    // Without staff.approve the tab Chờ duyệt belongs to "Danh sách nhân viên".
    ['/admin/staff', 'PENDING', ['staff.read'], 'staffList'],
    ['/admin/staff/0199a0f3-0000-7000-8000-000000000002', null, approver, 'staffList'],
    ['/admin/staff/new', null, approver, 'staffNew'],
    ['/admin/settings/business-location', null, ['business_location.update'], 'businessLocation'],
    ['/admin', null, approver, undefined],
  ])('%s ?status=%s', (pathname, status, permissions, key) => {
    expect(currentEntry(pathname, status, permissions)?.key).toBe(key)
  })
})

describe('breadcrumbOf', () => {
  const list = { key: 'admin.menu.staffList', href: '/admin/staff' }
  const approver = ['staff.read', 'staff.approve']
  it.each([
    ['/admin/staff', null, [{ key: 'admin.menu.staff' }, { key: 'admin.menu.staffList' }]],
    ['/admin/staff', 'PENDING', [{ key: 'admin.menu.staff' }, { key: 'admin.menu.staffPending' }]],
    ['/admin/staff/new', null, [{ key: 'admin.menu.staff' }, { key: 'admin.menu.staffNew' }]],
    [
      '/admin/settings/business-location',
      null,
      [{ key: 'admin.menu.settings' }, { key: 'admin.menu.businessLocation' }],
    ],
    ['/admin/account', null, [{ key: 'admin.user.account' }]],
    ['/admin', null, []],
    // A page under an entry (SCR-STF-03): the group, the entry as a link, then the page's title; any id.
    [
      '/admin/staff/0199a0f3-0000-7000-8000-000000000002',
      null,
      [{ key: 'admin.menu.staff' }, list, { key: 'staff.account.title' }],
    ],
    // An unknown path, the 404: no breadcrumb, also under an entry.
    ['/admin/staff/a/b', null, []],
    ['/admin/khong-ton-tai', null, []],
  ])('%s ?status=%s', (pathname, status, crumbs) => {
    expect(breadcrumbOf(pathname, status, approver)).toEqual(crumbs)
  })

  it('names the tab Chờ duyệt "Danh sách nhân viên" for a user without staff.approve', () => {
    expect(breadcrumbOf('/admin/staff', 'PENDING', ['staff.read'])).toEqual([
      { key: 'admin.menu.staff' },
      { key: 'admin.menu.staffList' },
    ])
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
