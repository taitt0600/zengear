import { getApps, initializeApp } from 'firebase/app'
import { connectAuthEmulator, getAuth, type Auth } from 'firebase/auth'
import { env } from '@/env'

let auth: Auth | undefined

/**
 * Firebase Authentication in the browser (docs/FRONTEND.md, Authentication; DEC-104 (10)): the SDK keeps the session
 * in its default browser storage, and development and CI connect to the Auth Emulator. Created on first use, in the
 * browser only.
 */
export function firebaseAuth(): Auth {
  if (auth) return auth
  const app =
    getApps()[0] ??
    initializeApp({
      apiKey: env.NEXT_PUBLIC_FIREBASE_API_KEY,
      authDomain: env.NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN,
      projectId: env.NEXT_PUBLIC_FIREBASE_PROJECT_ID,
      appId: env.NEXT_PUBLIC_FIREBASE_APP_ID,
    })
  auth = getAuth(app)
  if (env.NEXT_PUBLIC_FIREBASE_AUTH_EMULATOR_HOST) {
    connectAuthEmulator(auth, `http://${env.NEXT_PUBLIC_FIREBASE_AUTH_EMULATOR_HOST}`, {
      disableWarnings: true,
    })
  }
  return auth
}
