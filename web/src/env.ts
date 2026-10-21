import { parseEnv, publicEnvValues } from './env-schema'

/**
 * The checked NEXT_PUBLIC_* values of the app (src/env-schema.ts). The root layout imports this module, so a missing
 * or malformed value fails `next build` when it prerenders the pages; next typegen renders nothing and needs none.
 */
export const env = parseEnv(publicEnvValues())
