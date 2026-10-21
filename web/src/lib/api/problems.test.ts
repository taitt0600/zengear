import { describe, expect, it } from 'vitest'
import {
  ApiError,
  fieldErrors,
  problemMessage,
  problemName,
  unwrap,
  type Problem,
} from './problems'

const keys = new Set(['problems.account-disabled', 'problems.unknown'])
const hasKey = (key: string) => keys.has(key)

describe('problemMessage', () => {
  it('uses the message key of a known type', () => {
    const problem: Problem = {
      type: '/problems/account-disabled',
      detail: 'This account is not active.',
    }
    expect(problemMessage(problem, hasKey)).toEqual({ key: 'problems.account-disabled' })
  })

  it('falls back to detail for a type without a key', () => {
    const problem: Problem = { type: '/problems/brand-retired', detail: 'The brand is retired.' }
    expect(problemMessage(problem, hasKey)).toEqual({ text: 'The brand is retired.' })
  })

  it('shows the unknown message when there is neither a key nor a detail', () => {
    expect(problemMessage({ type: 'about:blank', status: 500 }, hasKey)).toEqual({
      key: 'problems.unknown',
    })
    expect(problemMessage(undefined, hasKey)).toEqual({ key: 'problems.unknown' })
  })

  it('never shows the ruleId', () => {
    const problem: Problem = { type: '/problems/brand-retired', ruleId: 'R-PRD-01' }
    expect(problemMessage(problem, hasKey)).toEqual({ key: 'problems.unknown' })
  })
})

describe('problemName', () => {
  it('reads the name of a /problems/ type only', () => {
    expect(problemName({ type: '/problems/permission-denied' })).toBe('permission-denied')
    expect(problemName({ type: 'https://example.com/problems/x' })).toBeUndefined()
    expect(problemName({})).toBeUndefined()
  })
})

describe('fieldErrors', () => {
  it('keeps the first message of each field, keyed like the form', () => {
    const problem: Problem = {
      status: 400,
      errors: {
        email: ['Email is required.', 'Email is invalid.'],
        name: [],
        employeeCode: ['Too long.'],
      },
    }
    expect(fieldErrors(problem)).toEqual({ email: 'Email is required.', employeeCode: 'Too long.' })
  })

  it('is empty for a problem without errors', () => {
    expect(fieldErrors({ status: 409 })).toEqual({})
  })
})

describe('unwrap', () => {
  const response = (status: number) => new Response(null, { status })

  it('returns the data of a success', () => {
    expect(unwrap({ data: { id: 1 }, response: response(200) })).toEqual({ id: 1 })
  })

  it('throws an ApiError with the status and the problem', () => {
    const problem = { type: '/problems/permission-denied', status: 403 }
    try {
      unwrap({ error: problem, response: response(403) })
      expect.unreachable()
    } catch (error) {
      expect(error).toBeInstanceOf(ApiError)
      expect(error).toMatchObject({ status: 403, problem })
    }
  })

  it('keeps no problem when the body was not one', () => {
    expect(() => unwrap({ error: 'Bad gateway', response: response(502) })).toThrow(
      expect.objectContaining({ status: 502, problem: undefined }),
    )
  })
})
