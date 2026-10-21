import { defineConfig } from 'vitest/config'

// Vite 8 compiles TSX itself and resolves the @/ alias of tsconfig.json: no plugin is needed.
export default defineConfig({
  resolve: { tsconfigPaths: true },
  test: {
    environment: 'jsdom',
    include: ['src/**/*.test.{ts,tsx}'],
    setupFiles: ['./vitest.setup.ts'],
  },
})
