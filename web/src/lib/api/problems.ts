import type { components } from './schema'

// Problem Details of the API (docs/API_CONVENTIONS.md, Errors; docs/FRONTEND.md, Forms and errors).
export type Problem = components['schemas']['ValidationProblemDetails']

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

function isProblem(value: unknown): value is Problem {
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

const PROBLEM_TYPE = /^\/problems\/([a-z0-9-]+)$/

/** The `/problems/<name>` part of a problem's type, or undefined for another type. */
export function problemName(problem: Problem | undefined): string | undefined {
  return problem?.type?.match(PROBLEM_TYPE)?.[1]
}

export type ProblemMessage = { key: `problems.${string}` } | { text: string }

/**
 * What to show for a problem: the message key `problems.<name>` when the message files have one, otherwise the
 * problem's `detail`, otherwise the key `problems.unknown`. `ruleId` is for logs and never shown. A 412 has no
 * problem type of its own (the API answers ASP.NET's default): its text comes from the status, at the caller.
 */
export function problemMessage(
  problem: Problem | undefined,
  hasKey: (key: string) => boolean,
): ProblemMessage {
  const name = problemName(problem)
  if (name && hasKey(`problems.${name}`)) return { key: `problems.${name}` }
  if (problem?.detail) return { text: problem.detail }
  return { key: 'problems.unknown' }
}

/** The first message of each field of a 400 validation problem, keyed like the form fields (camelCase). */
export function fieldErrors(problem: Problem | undefined): Record<string, string> {
  return Object.fromEntries(
    Object.entries(problem?.errors ?? {})
      .filter(([, messages]) => messages.length > 0)
      .map(([field, messages]) => [field, messages[0]]),
  )
}
