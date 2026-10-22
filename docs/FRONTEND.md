# Frontend

Structure of the ZenGear web app in `web/`. Stack: sheet `Tech Stack` of the blueprint. Decisions: DEC-093, DEC-104.

## One app, three parts

One Next.js app (App Router) serves the storefront, the admin pages and the POS, separated by route groups so each part has its own layout:

```
web/src/
├─ app/
│  ├─ (shop)/              storefront pages: home, category, product, cart, checkout, account (API /api/v1/shop/)
│  ├─ (admin)/admin/       back office: catalog, inventory, purchasing, orders, after-sales, cash book, reports
│  └─ (pos)/pos/           counter sales and shifts, full-screen layout
├─ features/<area>/        components, query hooks and Zod schemas per business area (same area names as the backend)
├─ components/ui/          shadcn/ui components as generated, never edited (.shadcn-lock.json: their hashes)
├─ components/app/         the app's own shared components, wrappers of components/ui included
├─ lib/api/                schema.d.ts (generated), client.ts (openapi-fetch), query keys, problem types → messages
├─ lib/auth/               Firebase Authentication client, current user, ID token
├─ lib/format/             VND money, dates in Asia/Ho_Chi_Minh (Intl only)
├─ i18n/                   next-intl request config
├─ env-schema.ts, env.ts   the NEXT_PUBLIC_* schema (Zod) and its checked values
└─ proxy.ts                fixed route redirects only (convenience, never authorization): / → /admin until the storefront
web/messages/vi.json       every text the user sees, by key (en.json later, storefront only)
web/e2e/                   Playwright screen flows against the API and the Firebase Auth Emulator
```

`features/` follows the business areas of `docs/ARCHITECTURE.md` (`catalog`, `inventory`, `sales`, `pos`, …) so a feature ID maps to the same area on both sides.

## Rendering

- Storefront catalog and product pages are rendered on the server so search engines see them; they call only shop endpoints (`/api/v1/shop/`).
- Admin and POS pages are client components behind sign-in; they call endpoints with the Firebase ID token.
- `url_redirects` (old slugs, 301) are resolved by the API; the storefront follows what the API returns.

## Data from the API

- Types: `npm run gen:api` runs openapi-typescript on `../api/openapi/ZenGear.Api.json` and writes `src/lib/api/schema.d.ts`. CI regenerates it and fails when the committed file differs.
- Calls: one openapi-fetch client, built by `src/lib/api/client.ts` and created once in `src/lib/api/api.ts`, with the base URL, the ID token (never sent to `/api/v1/shop/`), and two helpers of `src/lib/api/concurrency.ts` the caller passes: `If-Match` from the `ETag` of the loaded resource (`etagOf`), and a new `Idempotency-Key` per checkout or payment attempt (`newIdempotencyKey`, kept while that attempt is retried).
- Every list endpoint returns `PagedDto<T>` (`items`, `page`, `pageSize`, `totalCount`).
- Server state: TanStack Query hooks per area (`features/sales/queries.ts`); query keys in one place; after a command (`confirm`, `close shift`) invalidate the affected queries instead of editing the cache by hand.
- Paging: `page`, `pageSize`, `sort` as in `docs/API_CONVENTIONS.md`, kept in the URL search params so a list can be shared and reloaded.

## Forms and errors

- React Hook Form with a Zod schema per form; the schema checks input shape only (required, format, length) and mirrors the column limits in `docs/DATABASE.md`.
- A 400 validation problem is mapped onto the form fields by its `errors` keys (camelCase, same as the form fields).
- The message of an error comes from the problem's `type` (`/problems/stock-not-available`), mapped in `lib/api/` to a key of `messages/vi.json`; `detail` is shown only for a type without a key. `ruleId` is for logs, never shown.
- A 409 shows that message; a 412 tells the user the record changed and reloads it; 401 signs the user out; a 403 is handled by its `type` (`/problems/permission-denied` shows a no-permission page, `/problems/recent-login-required` signs the user in again and retries (DEC-094), the others as the Conventions of `docs/SCREENS.md` say).

## Text and languages

Every text the user sees goes through next-intl (without locale routing): `t('key')` with the keys of `web/messages/vi.json`, never a string written in JSX or in an attribute the user reads (`alt`, `aria-label`, `placeholder`, `title`, `label`…): ESLint refuses one (`react/jsx-no-literals` and `no-restricted-syntax`, tested by `src/lint.test.ts`), and a wrong key fails the typecheck (`src/global.d.ts`). CI checks that every message file has the same keys (`tools/message_keys.py`), so a storefront-only `en.json` needs a Decision on how the message files are split before it comes. Labels of business terms are the Vietnamese terms of `docs/GLOSSARY.md`. Phase 1 ships Vietnamese only. English comes later for the storefront only (`/en/...` with `hreflang`, `en.json`, an English label column in `docs/GLOSSARY.md`); the admin and the POS stay Vietnamese. Business content in two languages (product names, descriptions, categories, slugs) needs its own Decision; invoices and printed documents stay Vietnamese (DEC-104).

## Components and styling

`components/ui/` holds the shadcn/ui components exactly as `npm exec --no -- shadcn add` wrote them (the CLI pinned exactly in `web/package.json`, DEC-111 (2); `--no` fails instead of downloading). The style is `new-york` on Radix (`components.json`, written by hand in web-shell, since the `init` of shadcn 4.22 offers only other styles; its registry item `index` then installed the theme and the packages of DEC-111 (1)). A component that hard-codes English text a screen reader reads (the `sidebar`, the close button of `sheet`) is not used, or that text is turned off and replaced by one from the message files. The font is the system font stack, which covers Vietnamese without a download. They are themed only through the CSS variables of `app/globals.css` (`:root`, `@theme inline`) and `components.json`; a new variant or behaviour is a wrapper in `components/app/` or `features/<area>/`. `components/ui/.shadcn-lock.json` holds the sha256 of each file: with the DEC-104 tooling, CI fails when a file differs and changing the hash of an existing file is an owner-path change (an update with `add --overwrite` is its own `chore` pull request). Icons are named imports from `lucide-react`. Animation uses `motion` (`MotionConfig reducedMotion="user"` in the root layout, `LazyMotion`), minimal in the admin and the POS. Accessibility target: WCAG 2.2 AA.

## Authentication

Firebase Authentication in the browser (Phase 1): staff sign in to admin and POS; customers sign in on the storefront (Phase 2). The SDK keeps the session (its default persistence); there is no session cookie. Admin and POS pages are client components behind sign-in: the client layout of each part sends a visitor without a Firebase session to the sign-in page (the session lives in browser storage, which `proxy.ts` on the server cannot see). `proxy.ts` only makes fixed route redirects; neither is authorization, and the API still answers 401 and 403. The client only sends the ID token; the user's permission keys (never role names, R-SEC-03) come from the API (`permissions` of `GET /api/v1/admin/me`) and are used only to hide menus and buttons — the API still enforces them. A password change revokes every refresh token (DEC-094): the web signs the user in again. In development the client connects to the Firebase Auth Emulator.

## Security headers

`next.config` sets on every response (`src/lib/security-headers.ts`): a `Content-Security-Policy` that allows only this site, the API and the domains of Firebase Authentication and Storage (and the emulator when it is configured), with `frame-ancestors 'none'`; `Strict-Transport-Security` (without `includeSubDomains` until the production domain is chosen); `Referrer-Policy: strict-origin-when-cross-origin`; `X-Content-Type-Options: nosniff`. Static pages carry no nonce, so `script-src` holds `'unsafe-inline'` (and `'unsafe-eval'` in development only); a Decision on a nonce for `/admin` and `/pos` comes before the next admin screen (`docs/STATUS.md`). The ID token lives in the browser storage of the Firebase SDK, so the CSP is the main guard against script injection.

## Environment variables

| Variable | Content |
|---|---|
| `NEXT_PUBLIC_API_BASE_URL` | Base URL of `ZenGear.Api`, without a path: the paths of the contract start with `/api/v1/admin` for the admin and POS parts and `/api/v1/shop` for the storefront (DEC-106) |
| `NEXT_PUBLIC_FIREBASE_*` | Firebase web app config (public by design) |
| `NEXT_PUBLIC_FIREBASE_AUTH_EMULATOR_HOST` | Development and CI only: the Auth Emulator (`127.0.0.1:9099`) |

A Zod schema (`src/env-schema.ts`) checks these variables; the root layout imports the checked values (`src/env.ts`), so a missing or malformed one fails `next build` when it prerenders the pages (`next typegen` needs none). Outside a production build, the values of the development environment (`docs/ACCEPTANCE.md`: the API on the launch profile `http`, the emulator, project `demo-zengear`) are the defaults, so `npm run dev` needs no env file; a value set in the environment wins. Values per environment are set in Vercel and, for CI, in `.github/workflows/pipeline.yml`; no env file is committed.

## Packages

npm with `web/package-lock.json` committed; `web/.npmrc` sets `save-exact`, `engine-strict` (the `engines` range of `package.json`, the major of `.nvmrc`) and `ignore-scripts`, which build, tests and screen flows run with (DEC-104 (4)). Use the npm bundled with that Node.js, as CI does (`setup-node`): the lockfile of web-shell was written with it, and a newer npm installed globally may write it differently (not measured). A package is added only when `web/approved-packages.json` lists it (`web/CLAUDE.md`).

## Tests and checks

Vitest with Testing Library (jsdom; Vite compiles TSX itself, so no React plugin) for the Zod schemas, the mapping of Problem Details onto forms and messages, and the hooks; Playwright for the screen flows, run by `/finish-feature` (step 7) against the development environment and in the CI job `acceptance` against the API image and the Firebase Auth Emulator, with screenshots for the owner's test guide (DEC-100). ESLint (`eslint-config-next`), Prettier and `next typegen` + `tsc --noEmit` run locally (ESLint on each edited file and the typecheck at the end of a Claude Code turn, through the hooks of DEC-104) and in the CI job `web-test`, with the build, `npm run gen:api` compared with the committed `schema.d.ts`, the hashes of `components/ui/` and the keys of the message files. Business rules are tested in the API (`pr_gates` coverage counts API tests only).

## Screens

Screen list, per-screen fields and flows: `docs/SCREENS.md` (written before web work starts).
