import { describe, expect, it } from 'vitest'
import { initials } from './initials'

describe('initials', () => {
  it.each([
    ['Chủ cửa hàng', 'CH'],
    ['Nguyễn Văn Đức', 'NĐ'],
    ['  lan  ', 'L'],
    ['Quản lý (fixture)', 'QF'],
    ['', ''],
  ])('%s gives %s', (name, letters) => {
    expect(initials(name)).toBe(letters)
  })
})
