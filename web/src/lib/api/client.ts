import createClient, { type Middleware } from 'openapi-fetch'
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
