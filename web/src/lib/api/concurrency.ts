// Optimistic concurrency and idempotency (docs/API_CONVENTIONS.md, Concurrency and idempotency).

/**
 * The version of a loaded resource. An edit sends it back as its If-Match header parameter; the API answers 412
 * when the row changed since, and 428 when it is missing.
 */
export function etagOf(response: Response): string | undefined {
  return response.headers.get('ETag') ?? undefined
}

/**
 * A new Idempotency-Key for one attempt of a request that must not run twice (checkout, payments). The caller keeps
 * it while it retries that attempt, so the API returns the first result (R-ORD-12).
 */
export function newIdempotencyKey(): string {
  return crypto.randomUUID()
}
