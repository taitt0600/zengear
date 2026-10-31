import createClient, { type Middleware } from 'openapi-fetch'
import { ApiError, isProblem } from './problems'
import type { paths } from './schema'

export interface ApiClientOptions {
  /** Base URL of ZenGear.Api, without a trailing slash; the paths of the contract start with /api/v1. */
  baseUrl: string
  /** The Firebase ID token of the signed-in user, or null when nobody is signed in. */
  getIdToken: () => Promise<string | null>
  fetch?: typeof globalThis.fetch
}

// The anonymous shop endpoints never receive a token (docs/API_CONVENTIONS.md, Authentication).
const SHOP_PATH = '/api/v1/shop/'

/**
 * The one typed client of the API (web/CLAUDE.md): every call goes through it, so each request but those of the
 * shop endpoints carries the Firebase ID token. If-Match is a typed header parameter of each edit in the contract
 * (`params.header['If-Match']`, see concurrency.ts).
 */
export function createApiClient({ baseUrl, getIdToken, fetch }: ApiClientOptions) {
  const client = createClient<paths>({ baseUrl, fetch })
  const bearer: Middleware = {
    async onRequest({ request, schemaPath }) {
      if (schemaPath.startsWith(SHOP_PATH)) return request
      const token = await getIdToken()
      if (token) request.headers.set('Authorization', `Bearer ${token}`)
      return request
    },
  }
  client.use(bearer)
  return client
}

export type ApiClient = ReturnType<typeof createApiClient>

/** What a call of the generated client resolves with: the shape of its 2xx response types. */
export interface ApiResponse<T = unknown> {
  data: T
  status: number
  headers: Headers
}

async function readBody(response: Response): Promise<unknown> {
  const text = await response.text()
  if (!text) return undefined
  return response.headers.get('Content-Type')?.includes('json') ? JSON.parse(text) : text
}

/**
 * The mutator of the client Orval generates (DEC-122 (4)): it sends `url` (a path of the contract with its query)
 * to the API with the Firebase ID token, except to the shop endpoints, and resolves with the data, the status and
 * the headers of a 2xx answer, so that `etagOf` reads its ETag. Any other answer is thrown as an ApiError with its
 * Problem Details (DEC-117); a failed fetch or getIdToken is thrown as it is.
 */
export function createApiFetch({
  baseUrl,
  getIdToken,
  fetch = globalThis.fetch,
}: ApiClientOptions) {
  return async function apiFetch<T>(url: string, init: RequestInit = {}): Promise<T> {
    const headers = new Headers(init.headers)
    if (!url.startsWith(SHOP_PATH)) {
      const token = await getIdToken()
      if (token) headers.set('Authorization', `Bearer ${token}`)
    }
    const response = await fetch(`${baseUrl}${url}`, { ...init, headers })
    if (!response.ok) {
      // A body that does not parse is no problem: the answer still failed.
      const body = await readBody(response).catch(() => undefined)
      throw new ApiError(response.status, isProblem(body) ? body : undefined)
    }
    const data = await readBody(response)
    return { data, status: response.status, headers: response.headers } satisfies ApiResponse as T
  }
}
