import { describe, expect, it } from 'vitest'
import { DEVELOPMENT_DEFAULTS, parseEnv } from './env-schema'

const production = {
  NEXT_PUBLIC_API_BASE_URL: 'https://api.zengear.vn',
  NEXT_PUBLIC_FIREBASE_API_KEY: 'public-web-key',
  NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN: 'zengear.firebaseapp.com',
  NEXT_PUBLIC_FIREBASE_PROJECT_ID: 'zengear',
  NEXT_PUBLIC_STAFF_EMAIL_DOMAIN: 'zengear.vn',
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

  it('refuses a production build without the staff email domain', () => {
    expect(() =>
      parseEnv({ ...production, NEXT_PUBLIC_STAFF_EMAIL_DOMAIN: undefined }, true),
    ).toThrow(/NEXT_PUBLIC_STAFF_EMAIL_DOMAIN/)
  })

  it.each([
    ['a domain of a hyphenated label and a subdomain', 'mail.zen-gear.com.vn'],
    ['a domain of 190 characters', `${'a'.repeat(63)}.${'b'.repeat(63)}.${'c'.repeat(59)}.vn`],
  ])('accepts %s', (_, domain) => {
    expect(parseEnv({ ...production, NEXT_PUBLIC_STAFF_EMAIL_DOMAIN: domain }, true)).toEqual({
      ...production,
      NEXT_PUBLIC_STAFF_EMAIL_DOMAIN: domain,
    })
  })

  it.each([
    ['a domain with @', '@zengear.vn'],
    ['a domain without a dot', 'zengear'],
    ['a domain with a leading dot', '.zengear.vn'],
    ['a domain with a trailing dot', 'zengear.vn.'],
    ['a domain with a space', 'zen gear.vn'],
    ['a label that starts with a hyphen', '-zengear.vn'],
    ['a label that ends with a hyphen', 'zengear-.vn'],
    ['a label of 64 characters', `${'a'.repeat(64)}.vn`],
    ['a numeric top-level label', 'zengear.123'],
    ['a domain of 191 characters', `${'a'.repeat(63)}.${'b'.repeat(63)}.${'c'.repeat(60)}.vn`],
  ])('refuses %s as the staff email domain', (_, domain) => {
    expect(() => parseEnv({ ...production, NEXT_PUBLIC_STAFF_EMAIL_DOMAIN: domain }, true)).toThrow(
      /NEXT_PUBLIC_STAFF_EMAIL_DOMAIN/,
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

  it('signs staff in on the domain of the development API outside production', () => {
    // Staff:EmailDomain of api/src/ZenGear.Api/appsettings.Development.json (DEC-145).
    expect(parseEnv({}, false).NEXT_PUBLIC_STAFF_EMAIL_DOMAIN).toBe('zengear.test')
  })
})
