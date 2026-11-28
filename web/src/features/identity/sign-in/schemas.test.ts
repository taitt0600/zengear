import { describe, expect, it } from 'vitest'
import { signInSchema } from './schemas'

const schema = signInSchema({
  identifierRequired: 'required',
  emailInvalid: 'invalid',
  passwordRequired: 'password',
})
const errors = (values: { identifier: string; password: string }) =>
  schema.safeParse(values).error?.issues.map((issue) => issue.message) ?? []

describe('signInSchema', () => {
  it('trims the username or email and keeps the password as typed', () => {
    expect(schema.parse({ identifier: ' a@example.com ', password: ' mat khau ' })).toEqual({
      identifier: 'a@example.com',
      password: ' mat khau ',
    })
  })

  it('takes a username, a value without @', () => {
    expect(schema.parse({ identifier: ' HaLTZG260002 ', password: 'x' })).toEqual({
      identifier: 'HaLTZG260002',
      password: 'x',
    })
  })

  it.each([
    [{ identifier: '   ', password: 'x' }, ['required']],
    [{ identifier: 'haltzg260002@', password: 'x' }, ['invalid']],
    [{ identifier: '@zengear.test', password: 'x' }, ['invalid']],
    [{ identifier: 'a@example.com', password: '' }, ['password']],
    [{ identifier: '', password: '' }, ['required', 'password']],
  ])('refuses %o', (values, messages) => {
    expect(errors(values)).toEqual(messages)
  })
})
