import type { components } from './schema'

// Problem Details of the API (docs/API_CONVENTIONS.md, Errors; docs/FRONTEND.md, Forms and errors). The API writes
// `title`, `detail` and the `detail` of each `errors` item in Vietnamese (DEC-117): the web shows them as they come and
// never maps a type or a code to a text of its own.
export type Problem = components['schemas']['ValidationProblem']

/** A failed API call: its HTTP status and, when the body was one, its Problem Details. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly problem?: Problem,
  ) {
    super(problem?.type ?? `HTTP ${status}`)
    this.name = 'ApiError'
  }
}

/** Whether a parsed body is Problem Details (it has a type, a status or a title). */
export function isProblem(value: unknown): value is Problem {
  return (
    typeof value === 'object' &&
    value !== null &&
    ('type' in value || 'status' in value || 'title' in value)
  )
}

/** The data of an openapi-fetch result, or an ApiError for any answer that is not a success. */
export function unwrap<T>(result: { data?: T; error?: unknown; response: Response }): T {
  if (result.response.ok) return result.data as T
  throw new ApiError(result.response.status, isProblem(result.error) ? result.error : undefined)
}

/**
 * Whether a problem has the type `/problems/<name>`: for behaviour only (which page or dialog follows), never to
 * choose a text.
 */
export function hasProblemType(problem: Problem | undefined, name: string): boolean {
  return problem?.type === `/problems/${name}`
}

/**
 * Whether a call got no answer at all: fetch fails with a TypeError, and Firebase (getIdToken in the API client, or a
 * sign-in) with the code auth/network-request-failed.
 */
export function isNetworkError(error: unknown): boolean {
  if (error instanceof TypeError) return true
  return (
    typeof error === 'object' &&
    error !== null &&
    'code' in error &&
    error.code === 'auth/network-request-failed'
  )
}

/** The web's own texts of a failed call, for when the API gave none. */
export type ProblemTextKey = 'problems.network' | 'problems.unknown'

/** What to show for a failed call: the API's text, or the key of the web's own text. */
export type ProblemText = { text: string } | { key: ProblemTextKey }

/**
 * What to show for a failed call: the problem's `detail`; without an answer, the key `problems.network`; for an
 * answer without a `detail` (a 500, a body that is not a problem) or any other error, the key `problems.unknown`.
 * `ruleId` is for logs and never shown.
 */
export function problemText(error: unknown): ProblemText {
  if (isNetworkError(error)) return { key: 'problems.network' }
  const detail = error instanceof ApiError ? error.problem?.detail : undefined
  return detail ? { text: detail } : { key: 'problems.unknown' }
}

/** The texts of a 400 (or a 409 bound to a field): one per form field, and those no field of the form shows. */
export interface FieldErrors<F extends string> {
  byField: Partial<Record<F, string>>
  other: string[]
}

// A JSON Pointer as a URI fragment (`#/regularHours/1/day`) as the path of a form field (`regularHours.1.day`).
function pointerPath(pointer: string): string | undefined {
  if (!pointer.startsWith('#/')) return undefined
  return pointer
    .slice(2)
    .split('/')
    .map((segment) => segment.replaceAll('~1', '/').replaceAll('~0', '~'))
    .join('.')
}

/**
 * The `errors` of a problem on the fields of a form: the first `detail` of each field the form has, matched by
 * `pointer` (`#/name`, a body field) or `parameter` (`roleId`, a query or route parameter); every other item goes to
 * `other`, for the form to show at its top.
 */
export function fieldErrors<F extends string>(
  problem: Problem | undefined,
  fields: readonly F[],
): FieldErrors<F> {
  const byField: Partial<Record<F, string>> = {}
  const other: string[] = []
  for (const item of problem?.errors ?? []) {
    const path = item.pointer === undefined ? item.parameter : pointerPath(item.pointer)
    const field = fields.find((name) => name === path)
    if (field === undefined) {
      if (!other.includes(item.detail)) other.push(item.detail)
    } else {
      byField[field] ??= item.detail
    }
  }
  return { byField, other }
}
