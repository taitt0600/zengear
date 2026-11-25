import { describe, expect, it } from 'vitest'
import { initials } from './initials'

describe('initials', () => {
  it.each([
    ['Trần Minh Khoa', 'TK'],
    ['Nguyễn Văn Đức', 'NĐ'],
    ['  lan  ', 'L'],
    ['Lê Thu Hà', 'LH'],
    ['', ''],
  ])('%s gives %s', (name, letters) => {
    expect(initials(name)).toBe(letters)
  })
})
