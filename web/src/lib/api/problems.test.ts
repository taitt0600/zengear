import { describe, expect, it } from 'vitest'
import {
  ApiError,
  fieldErrors,
  hasProblemType,
  isNetworkError,
  problemText,
  type Problem,
} from './problems'

describe('problemText', () => {
  it("shows the problem's detail", () => {
    const error = new ApiError(409, {
      type: '/problems/brand-retired',
      status: 409,
      detail: 'Thương hiệu đã ngừng dùng.',
    })
    expect(problemText(error)).toEqual({ text: 'Thương hiệu đã ngừng dùng.' })
  })

  it('says the server could not be reached when there was no answer', () => {
    expect(problemText(new TypeError('Failed to fetch'))).toEqual({ key: 'problems.network' })
    const firebase = Object.assign(new Error('offline'), { code: 'auth/network-request-failed' })
    expect(problemText(firebase)).toEqual({ key: 'problems.network' })
  })

  it('shows the unknown text for a 500, a body that is not a problem or another error', () => {
    expect(
      problemText(new ApiError(500, { type: '/problems/internal-error', status: 500 })),
    ).toEqual({ key: 'problems.unknown' })
    expect(problemText(new ApiError(502))).toEqual({ key: 'problems.unknown' })
    expect(problemText(new Error('boom'))).toEqual({ key: 'problems.unknown' })
  })

  it('never shows the title, the type or the ruleId', () => {
    const error = new ApiError(409, {
      type: '/problems/brand-retired',
      title: 'Thương hiệu đã ngừng dùng',
      ruleId: 'R-DEL-01',
    })
    expect(problemText(error)).toEqual({ key: 'problems.unknown' })
  })
})

describe('hasProblemType', () => {
  it('compares the /problems/ type exactly', () => {
    expect(hasProblemType({ type: '/problems/permission-denied' }, 'permission-denied')).toBe(true)
    expect(hasProblemType({ type: '/problems/permission-denied-x' }, 'permission-denied')).toBe(
      false,
    )
    expect(hasProblemType({ type: 'https://example.com/problems/x' }, 'x')).toBe(false)
    expect(hasProblemType(undefined, 'x')).toBe(false)
  })
})

describe('isNetworkError', () => {
  it('is a failed fetch or a Firebase network failure, nothing else', () => {
    expect(isNetworkError(new TypeError('Failed to fetch'))).toBe(true)
    expect(isNetworkError({ code: 'auth/network-request-failed' })).toBe(true)
    expect(isNetworkError({ code: 'auth/internal-error' })).toBe(false)
    expect(isNetworkError(new ApiError(503))).toBe(false)
  })
})

describe('fieldErrors', () => {
  const item = (where: { pointer?: string; parameter?: string }, detail: string) => ({
    ...where,
    code: 'CODE',
    detail,
  })

  it('takes a body field by its pointer, the first detail of each field', () => {
    const problem: Problem = {
      status: 400,
      errors: [
        item({ pointer: '#/name' }, 'Vui lòng nhập tên.'),
        item({ pointer: '#/name' }, 'Tên tối đa 100 ký tự.'),
        item({ pointer: '#/email' }, 'Email không hợp lệ.'),
      ],
    }
    expect(fieldErrors(problem, ['name', 'email'])).toEqual({
      byField: { name: 'Vui lòng nhập tên.', email: 'Email không hợp lệ.' },
      other: [],
    })
  })

  it('takes a query or route parameter by its name', () => {
    const problem: Problem = {
      status: 400,
      errors: [item({ parameter: 'roleId' }, 'Giá trị không hợp lệ.')],
    }
    expect(fieldErrors(problem, ['roleId'])).toEqual({
      byField: { roleId: 'Giá trị không hợp lệ.' },
      other: [],
    })
  })

  it('reads a nested pointer as the dotted path of a form field', () => {
    const problem: Problem = {
      status: 400,
      errors: [item({ pointer: '#/regularHours/1/day' }, 'Vui lòng chọn thứ trong tuần.')],
    }
    expect(fieldErrors(problem, ['regularHours.1.day']).byField).toEqual({
      'regularHours.1.day': 'Vui lòng chọn thứ trong tuần.',
    })
  })

  it('gives the items no field of the form shows to other, once each', () => {
    const problem: Problem = {
      status: 400,
      errors: [
        item({ pointer: '#' }, 'Thiếu nội dung yêu cầu.'),
        item({ pointer: '#/joinedDate' }, 'Vui lòng nhập ngày vào làm.'),
        item({ parameter: 'page' }, 'Số trang phải từ 1 đến 21.474.836.'),
        item({ pointer: '#/name' }, 'Vui lòng nhập tên.'),
        item({}, 'Vui lòng nhập ngày vào làm.'),
      ],
    }
    expect(fieldErrors(problem, ['name'])).toEqual({
      byField: { name: 'Vui lòng nhập tên.' },
      other: [
        'Thiếu nội dung yêu cầu.',
        'Vui lòng nhập ngày vào làm.',
        'Số trang phải từ 1 đến 21.474.836.',
      ],
    })
  })

  it('is empty for a problem without errors', () => {
    expect(fieldErrors({ status: 409 }, ['name'])).toEqual({ byField: {}, other: [] })
    expect(fieldErrors(undefined, ['name'])).toEqual({ byField: {}, other: [] })
  })
})
