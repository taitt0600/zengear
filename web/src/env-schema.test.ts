import { describe, expect, it } from 'vitest'
import { DEVELOPMENT_DEFAULTS, parseEnv } from './env-schema'

const production = {
  NEXT_PUBLIC_API_BASE_URL: 'https://api.zengear.vn',
  NEXT_PUBLIC_FIREBASE_API_KEY: 'public-web-key',
  NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN: 'zengear.firebaseapp.com',
  NEXT_PUBLIC_FIREBASE_PROJECT_ID: 'zengear',
}

describe('parseEnv', () => {
  it('accepts a production configuration without the emulator', () => {
    expect(parseEnv(production, true)).toEqual(production)
  })

  it('refuses a production build with a missing value', () => {
    expect(() => parseEnv({ ...production, NEXT_PUBLIC_FIREBASE_PROJECT_ID: '' }, true)).toThrow(
      /NEXT_PUBLIC_FIREBASE_PROJECT_ID/,
    )
  })

  it.each([
    ['an API URL with a trailing slash', { NEXT_PUBLIC_API_BASE_URL: 'https://api.zengear.vn/' }],
    ['an API URL that is not http', { NEXT_PUBLIC_API_BASE_URL: 'ftp://api.zengear.vn' }],
    ['an emulator host without a port', { NEXT_PUBLIC_FIREBASE_AUTH_EMULATOR_HOST: 'localhost' }],
    [
      'an emulator host with a scheme',
      { NEXT_PUBLIC_FIREBASE_AUTH_EMULATOR_HOST: 'http://localhost:9099' },
    ],
  ])('refuses %s', (_, wrong) => {
    expect(() => parseEnv({ ...production, ...wrong }, true)).toThrow(/Invalid NEXT_PUBLIC_/)
  })

  it('fills the development values outside production, a set value winning', () => {
    expect(parseEnv({ NEXT_PUBLIC_API_BASE_URL: 'http://localhost:8180' }, false)).toEqual({
      ...DEVELOPMENT_DEFAULTS,
      NEXT_PUBLIC_API_BASE_URL: 'http://localhost:8180',
    })
  })
})
