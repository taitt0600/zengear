import { defineConfig } from 'orval'

// The API client generated from the contract (DEC-122 (4)): npm run gen:api writes src/lib/api/generated/, which
// changes only through it, one folder per tag of the contract and the models in model/. Every call goes through the
// mutator src/lib/api/api.ts, which throws an ApiError for any answer that is not a success, so the generated return
// types hold only the successes (forceSuccessResponse).
export default defineConfig({
  zengear: {
    input: { target: '../api/openapi/ZenGear.Api.json' },
    output: {
      target: 'src/lib/api/generated/endpoints.ts',
      schemas: 'src/lib/api/generated/model',
      mode: 'tags-split',
      client: 'react-query',
      httpClient: 'fetch',
      headers: true,
      clean: true,
      override: {
        mutator: { path: 'src/lib/api/api.ts', name: 'apiFetch' },
        fetch: { forceSuccessResponse: true },
      },
    },
  },
})
