# Frontend

Structure of the ZenGear web app in `web/`. Stack: sheet `Tech Stack` of the blueprint. Decisions: DEC-093, DEC-104.

## One app, three parts

One Next.js app (App Router) serves the storefront, the admin pages and the POS, separated by route groups so each part has its own layout:

```
web/src/
├─ app/
│  ├─ (storefront)/        public pages: home, category, product, cart, checkout, account
│  ├─ (admin)/admin/       back office: catalog, inventory, purchasing, orders, after-sales, cash book, reports
│  └─ (pos)/pos/           counter sales and shifts, full-screen layout
├─ features/<area>/        components, query hooks and Zod schemas per business area (same area names as the backend)
├─ components/ui/          shadcn/ui components as generated, never edited (.shadcn-lock.json: their hashes)
├─ components/app/         the app's own shared components, wrappers of components/ui included
├─ lib/api/                schema.d.ts (generated), client.ts (openapi-fetch), query keys, problem types → messages
├─ lib/auth/               Firebase Authentication client, current user, ID token
├─ lib/format/             VND money, dates in Asia/Ho_Chi_Minh (Intl only)
├─ i18n/                   next-intl request config
└─ proxy.ts                fixed route redirects only (convenience, never authorization)
web/messages/vi.json       every text the user sees, by key (en.json later, storefront only)
```

`features/` follows the business areas of `docs/ARCHITECTURE.md` (`catalog`, `inventory`, `sales`, `pos`, …) so a feature ID maps to the same area on both sides.

## Rendering

- Storefront catalog and product pages are rendered on the server so search engines see them; they call only public endpoints.
- Admin and POS pages are client components behind sign-in; they call endpoints with the Firebase ID token.
- `url_redirects` (old slugs, 301) are resolved by the API; the storefront follows what the API returns.

## Data from the API

- Types: `npm run gen:api` runs openapi-typescript on `../api/openapi/ZenGear.Api.json` and writes `src/lib/api/schema.d.ts`. CI regenerates it and fails when the committed file differs.
- Calls: one openapi-fetch client in `src/lib/api/client.ts` with the base URL, the ID token, `If-Match` from the `ETag` of the loaded resource, and a new `Idempotency-Key` per checkout or payment attempt (kept while that attempt is retried).
- Every list endpoint returns `PagedDto<T>` (`items`, `page`, `pageSize`, `totalCount`).
- Server state: TanStack Query hooks per area (`features/sales/queries.ts`); query keys in one place; after a command (`confirm`, `close shift`) invalidate the affected queries instead of editing the cache by hand.
- Paging: `page`, `pageSize`, `sort` as in `docs/API_CONVENTIONS.md`, kept in the URL search params so a list can be shared and reloaded.

## Forms and errors

- React Hook Form with a Zod schema per form; the schema checks input shape only (required, format, length) and mirrors the column limits in `docs/DATABASE.md`.
- A 400 validation problem is mapped onto the form fields by its `errors` keys (camelCase, same as the form fields).
- The message of an error comes from the problem's `type` (`/problems/stock-not-available`), mapped in `lib/api/` to a key of `messages/vi.json`; `detail` is shown only for a type without a key. `ruleId` is for logs, never shown.
- A 409 shows that message; a 412 tells the user the record changed and reloads it; 401 signs the user out; a 403 is handled by its `type` (`/problems/permission-denied` shows a no-permission page, `/problems/recent-login-required` signs the user in again and retries (DEC-094), the others as the Conventions of `docs/SCREENS.md` say).

## Text and languages

Every text the user sees goes through next-intl: `t('key')` with the keys of `web/messages/vi.json`, never a string written in JSX (with the DEC-104 tooling, a lint rule refuses one and CI checks that every message file has the same keys). Labels of business terms are the Vietnamese terms of `docs/GLOSSARY.md`. Phase 1 ships Vietnamese only. English comes later for the storefront only (`/en/...` with `hreflang`, `en.json`, an English label column in `docs/GLOSSARY.md`); the admin and the POS stay Vietnamese. Business content in two languages (product names, descriptions, categories, slugs) needs its own Decision; invoices and printed documents stay Vietnamese (DEC-104).

## Components and styling

`components/ui/` holds the shadcn/ui components exactly as `npx shadcn@<pinned version> add` wrote them. They are themed only through the CSS variables of `app/globals.css` (`:root`, `@theme inline`) and `components.json`; a new variant or behaviour is a wrapper in `components/app/` or `features/<area>/`. `components/ui/.shadcn-lock.json` holds the sha256 of each file: with the DEC-104 tooling, CI fails when a file differs and changing the hash of an existing file is an owner-path change (an update with `add --overwrite` is its own `chore` pull request). Icons are named imports from `lucide-react`. Animation uses `motion` (`MotionConfig reducedMotion="user"` in the root layout, `LazyMotion`), minimal in the admin and the POS. Accessibility target: WCAG 2.2 AA.

## Authentication

Firebase Authentication in the browser (Phase 1): staff sign in to admin and POS; customers sign in on the storefront (Phase 2). The SDK keeps the session (its default persistence); there is no session cookie. Admin and POS pages are client components behind sign-in: the client layout of each part sends a visitor without a Firebase session to the sign-in page (the session lives in browser storage, which `proxy.ts` on the server cannot see). `proxy.ts` only makes fixed route redirects; neither is authorization, and the API still answers 401 and 403. The client only sends the ID token; roles and permissions come from the API (`GET /api/v1/me`) and are used to hide menus — the API still enforces them. A password change revokes every refresh token (DEC-094): the web signs the user in again. In development the client connects to the Firebase Auth Emulator.

## Security headers

`next.config` sets on every response: a `Content-Security-Policy` that allows only the API and the domains of Firebase Authentication and Storage; `Strict-Transport-Security`; `frame-ancestors 'none'`; `Referrer-Policy`. The ID token lives in the browser storage of the Firebase SDK, so the CSP is the main guard against script injection.

## Environment variables

| Variable | Content |
|---|---|
| `NEXT_PUBLIC_API_BASE_URL` | Base URL of `ZenGear.Api` (`…/api/v1` is added by the client) |
| `NEXT_PUBLIC_FIREBASE_*` | Firebase web app config (public by design) |
| `NEXT_PUBLIC_FIREBASE_AUTH_EMULATOR_HOST` | Development only: the Auth Emulator (`localhost:9099`) |

A Zod schema checks these variables when the app builds; a missing or malformed one fails the build. Values per environment are set in Vercel; `.env.local` for local work is not committed.

## Tests and checks

Vitest with Testing Library (jsdom) for the Zod schemas, the mapping of Problem Details onto forms and messages, and the hooks; Playwright for the screen flows, run in the CI job `acceptance` against the API image and the Firebase Auth Emulator, with screenshots for the owner's test guide (DEC-100). ESLint (`eslint-config-next`), Prettier and `tsc --noEmit` run locally (ESLint on each edited file and `tsc` at the end of a Claude Code turn, through the hooks of DEC-104) and in the CI job `web-test` that the DEC-104 tooling adds, with the build, `npm run gen:api` compared with the committed `schema.d.ts`, the hashes of `components/ui/` and the keys of the message files. Business rules are tested in the API (`pr_gates` coverage counts API tests only).

## Screens

Screen list, per-screen fields and flows: `docs/SCREENS.md` (written before web work starts).
