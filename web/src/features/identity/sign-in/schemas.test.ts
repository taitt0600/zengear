import { describe, expect, it } from 'vitest'
import { signInSchema } from './schemas'

const schema = signInSchema({
  emailRequired: 'required',
  emailInvalid: 'invalid',
  passwordRequired: 'password',
})
const errors = (values: { email: string; password: string }) =>
  schema.safeParse(values).error?.issues.map((issue) => issue.message) ?? []

describe('signInSchema', () => {
  it('trims the email and keeps the password as typed', () => {
    expect(schema.parse({ email: ' a@example.com ', password: ' mat khau ' })).toEqual({
      email: 'a@example.com',
      password: ' mat khau ',
    })
  })

  it.each([
    [{ email: '   ', password: 'x' }, ['required']],
    [{ email: 'nhanvien', password: 'x' }, ['invalid']],
    [{ email: 'a@example.com', password: '' }, ['password']],
  ])('refuses %o', (values, messages) => {
    expect(errors(values)).toEqual(messages)
  })
})
