import { z } from 'zod'

// The public configuration of the web (docs/FRONTEND.md, Environment variables; DEC-104 (10)): its schema, without a
// side effect, so that next.config.ts can import it (src/env.ts holds the checked values). Only public values: never
// a secret.
const HOST_AND_PORT = /^[A-Za-z0-9.-]+:[0-9]{2,5}$/
// The rule of StaffIdentityOptions.EmailDomain of the API (api/src/ZenGear.Services/Identity/StaffAccounts/): a host name
// of labels of letters, digits and hyphens with at least one dot, at most 190 characters. A copy, since C# and
// TypeScript share no code: the two change together.
const EMAIL_DOMAIN = /^(?=.{1,190}$)([A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?\.)+[A-Za-z]{2,63}$/

const envSchema = z.object({
  NEXT_PUBLIC_API_BASE_URL: z
    .url({ protocol: /^https?$/ })
    .refine((url) => !url.endsWith('/'), { error: 'no trailing slash' }),
  NEXT_PUBLIC_FIREBASE_API_KEY: z.string().min(1),
  NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN: z.string().min(1),
  NEXT_PUBLIC_FIREBASE_PROJECT_ID: z.string().min(1),
  NEXT_PUBLIC_FIREBASE_APP_ID: z.string().min(1).optional(),
  // Development and CI only: the Firebase Auth Emulator, as host:port.
  NEXT_PUBLIC_FIREBASE_AUTH_EMULATOR_HOST: z.string().regex(HOST_AND_PORT).optional(),
  // The domain of the company emails (R-USR-24), on which the sign-in of SCR-AUT-01 turns a username into its email:
  // the value of Staff:EmailDomain of the API in the same environment (DEC-145).
  NEXT_PUBLIC_STAFF_EMAIL_DOMAIN: z.string().regex(EMAIL_DOMAIN),
})

export type Env = z.infer<typeof envSchema>

// The development environment of docs/ACCEPTANCE.md: the API on the launch profile http and the Firebase Auth
// Emulator, whose project demo-zengear accepts any API key. Used only outside a production build, so that next dev
// runs after dev_reset without an env file; a production build needs every value set (Vercel, CI).
export const DEVELOPMENT_DEFAULTS: Env = {
  NEXT_PUBLIC_API_BASE_URL: 'http://localhost:5256',
  NEXT_PUBLIC_FIREBASE_API_KEY: 'demo-api-key',
  NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN: 'demo-zengear.firebaseapp.com',
  NEXT_PUBLIC_FIREBASE_PROJECT_ID: 'demo-zengear',
  NEXT_PUBLIC_FIREBASE_AUTH_EMULATOR_HOST: '127.0.0.1:9099',
  // Staff:EmailDomain of api/src/ZenGear.Api/appsettings.Development.json.
  NEXT_PUBLIC_STAFF_EMAIL_DOMAIN: 'zengear.test',
}

export function parseEnv(
  values: Record<string, string | undefined>,
  production = process.env.NODE_ENV === 'production',
): Env {
  // An empty variable counts as unset.
  const set = Object.fromEntries(Object.entries(values).filter(([, value]) => value))
  const result = envSchema.safeParse(production ? set : { ...DEVELOPMENT_DEFAULTS, ...set })
  if (!result.success) {
    throw new Error(`Invalid NEXT_PUBLIC_* variables:\n${z.prettifyError(result.error)}`)
  }
  return result.data
}

// Each variable is written out in full: Next.js inlines only literal process.env.NEXT_PUBLIC_* references.
export function publicEnvValues(): Record<string, string | undefined> {
  return {
    NEXT_PUBLIC_API_BASE_URL: process.env.NEXT_PUBLIC_API_BASE_URL,
    NEXT_PUBLIC_FIREBASE_API_KEY: process.env.NEXT_PUBLIC_FIREBASE_API_KEY,
    NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN: process.env.NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN,
    NEXT_PUBLIC_FIREBASE_PROJECT_ID: process.env.NEXT_PUBLIC_FIREBASE_PROJECT_ID,
    NEXT_PUBLIC_FIREBASE_APP_ID: process.env.NEXT_PUBLIC_FIREBASE_APP_ID,
    NEXT_PUBLIC_FIREBASE_AUTH_EMULATOR_HOST: process.env.NEXT_PUBLIC_FIREBASE_AUTH_EMULATOR_HOST,
    NEXT_PUBLIC_STAFF_EMAIL_DOMAIN: process.env.NEXT_PUBLIC_STAFF_EMAIL_DOMAIN,
  }
}
