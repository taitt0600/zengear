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
├─ components/ui/          shadcn/ui components, code of the app (open code, DEC-116)
├─ components/app/         the app's own shared components (compositions of components/ui, files of blocks)
├─ lib/api/                schema.d.ts (generated), client.ts (openapi-fetch), query keys, problems.ts (errors → fields)
├─ lib/auth/               Firebase Authentication client, current user, ID token
├─ lib/format/             VND money, dates in Asia/Ho_Chi_Minh (Intl only; date-fns only in the calendar and date pickers)
├─ i18n/                   next-intl request config
├─ env-schema.ts, env.ts   the NEXT_PUBLIC_* schema (Zod) and its checked values
└─ proxy.ts                fixed route redirects (/ → /admin until the storefront) and the CSP with a nonce of /admin and /pos; never the session, never authorization
web/messages/vi.json       every text the user sees, by key (en.json later, storefront only)
web/e2e/                   Playwright screen flows against the API and the Firebase Auth Emulator
```

`features/` follows the business areas of `docs/ARCHITECTURE.md` (`catalog`, `inventory`, `sales`, `pos`, …) so a feature ID maps to the same area on both sides.

## Rendering

- Storefront catalog and product pages are rendered on the server so search engines see them; they call only shop endpoints (`/api/v1/shop/`).
- Admin and POS pages are client components behind sign-in; they call endpoints with the Firebase ID token. The root layout of `/admin` (and of `/pos` when it comes) awaits `connection()` (`next/server`), so every page of `/admin` and `/pos` renders on the server at each request and gets the nonce of its CSP (DEC-113 (3)): they are not cached by a CDN and cannot be exported statically, so the web runs on `next start` or a platform with a server (Vercel).
- `url_redirects` (old slugs, 301) are resolved by the API; the storefront follows what the API returns.

## Data from the API

- Types: `npm run gen:api` runs openapi-typescript on `../api/openapi/ZenGear.Api.json` and writes `src/lib/api/schema.d.ts`. CI regenerates it and fails when the committed file differs.
- Calls: one openapi-fetch client, built by `src/lib/api/client.ts` and created once in `src/lib/api/api.ts`, with the base URL, the ID token (never sent to `/api/v1/shop/`), and two helpers of `src/lib/api/concurrency.ts` the caller passes: `If-Match` from the `ETag` of the loaded resource (`etagOf`), and a new `Idempotency-Key` per checkout or payment attempt (`newIdempotencyKey`, kept while that attempt is retried).
- Every list endpoint returns `PagedDto<T>` (`items`, `page`, `pageSize`, `totalCount`).
- Server state: TanStack Query hooks per area (`features/sales/queries.ts`); query keys in one place; after a command (`confirm`, `close shift`) invalidate the affected queries instead of editing the cache by hand.
- Paging: `page`, `pageSize`, `sort` as in `docs/API_CONVENTIONS.md`, kept in the URL search params so a list can be shared and reloaded.

## Forms and errors

- React Hook Form with a Zod schema per form; the schema checks input shape only (required, format, length) and mirrors the column limits in `docs/DATABASE.md`.
- The web shows the API's texts and decides nothing about an error (DEC-117 (4)): the `detail` of each item of `errors` goes under the field whose name its `pointer` (`#/name`) or `parameter` (`roleId`) gives, and an item no field shows goes in an alert at the top of the form (`fieldErrors` of `lib/api/problems.ts`, `components/app/form-problems.tsx`); any other problem shows its `detail` in a toast or on the page (`problemText`). The web never maps a `type`, a `code` or a status to a text of its own; `ruleId` is for logs, never shown.
- For an answer of the API, `messages/vi.json` holds only two texts of the web itself: `problems.network` (no answer) and `problems.unknown` (an answer that cannot be read, or a 500, which has no `detail`). Errors of Firebase Authentication happen in the browser and never reach the API: they are mapped by the `auth/*` codes Firebase publishes. Zod checks only the shape of the input before sending (DEC-104) and gives no text to an error the API answers.
- `type` and `code` only choose a behaviour: a 412 or 428 shows its `detail` and reloads the record; 401 signs the user out; a 403 by its `type` (`/problems/permission-denied` shows a no-permission page, `/problems/recent-login-required` signs the user in again and retries (DEC-094), the others as the Conventions of `docs/SCREENS.md` say).

## Text and languages

Every text the user sees goes through next-intl (without locale routing), except the texts of the errors the API answers, shown as they come (DEC-117 (4)): `t('key')` with the keys of `web/messages/vi.json`, never a string written in JSX or in an attribute the user reads (`alt`, `aria-label`, `placeholder`, `title`, `label`…): ESLint refuses one, in `components/ui/` too (`react/jsx-no-literals` and `no-restricted-syntax`, tested by `src/lint.test.ts`); a text that the package of a shadcn component shows (sonner, react-day-picker, cmdk, vaul) is set through its prop or locale from the message files, which `code-reviewer` checks since ESLint cannot see it (DEC-116 (2)); and a wrong key fails the typecheck (`src/global.d.ts`). CI checks that every message file has the same keys (`tools/message_keys.py`), so a storefront-only `en.json` needs a Decision on how the message files are split before it comes. Labels of business terms are the Vietnamese terms of `docs/GLOSSARY.md`. Phase 1 ships Vietnamese only. English comes later for the storefront only (`/en/...` with `hreflang`, `en.json`, an English label column in `docs/GLOSSARY.md`); the admin and the POS stay Vietnamese. Business content in two languages (product names, descriptions, categories, slugs) needs its own Decision; invoices and printed documents stay Vietnamese (DEC-104).

## Components and styling

`components/ui/` holds the shadcn/ui components as code of the app (open code, DEC-116 (1)): added with `npm exec --no -- shadcn add` (the CLI pinned exactly in `web/package.json`, DEC-111 (2); `--no` fails instead of downloading), then edited like other code, also in a feature pull request; in a pull request with an `add`, the first commit holds only what `add` wrote and the edits come after, so that git keeps the registry's version. The style is `new-york` on Radix (`components.json`, written by hand in web-shell, since the `init` of shadcn 4.22 offers only other styles; its registry item `index` then installed the theme and the packages of DEC-111 (1)). Every item of the official registry `@shadcn` may be used, also one that writes English (the `sidebar`, the close button of `sheet`): in the pull request of its `add`, its texts go through `t(key)` of the namespace `ui`; an item that needs `@base-ui/react` or `@shadcn/react` is not used, and a third-party registry needs a Decision of its own (DEC-116 (2), (7)). The font is the system font stack, which covers Vietnamese without a download. The design of shadcn is kept: the classes that set the look of a component (colour, spacing, radius, font size, shadow) change only to fix accessibility; the look changes through the CSS variables of `app/globals.css` (`:root`, `@theme inline`) and `components.json`, and a new variant uses the tokens of the theme. A component in `components/app/` or `features/<area>/` composes several of them. A block of `@shadcn` (`sidebar-*`, `login-*`, `dashboard-01`…) is a template: its files move to `features/<area>/` or `components/app/`, without its sample page, data and texts, and are never updated again from the registry (DEC-116 (3)). An update of an installed component is a `chore(web)` pull request of its own: `add --diff` against the registry, `add --overwrite` only when none of the files it would write was edited, otherwise the registry's changes applied by hand (DEC-116 (4); `components/ui/` stays out of Prettier so that the diff stays small). Icons are named imports from `lucide-react`; a block that imports another icon set is switched to them. Animation uses `motion` (`MotionConfig reducedMotion="user"` in the root layout, `LazyMotion`), minimal in the admin and the POS. Accessibility target: WCAG 2.2 AA.

## Authentication

Firebase Authentication in the browser (Phase 1): staff sign in to admin and POS; customers sign in on the storefront (Phase 2). The SDK keeps the session (its default persistence); there is no session cookie. Admin and POS pages are client components behind sign-in: the client layout of each part sends a visitor without a Firebase session to the sign-in page (the session lives in browser storage, which `proxy.ts` on the server cannot see). `proxy.ts` makes fixed route redirects and sets the CSP of `/admin` and `/pos`, and never reads the session; neither is authorization, and the API still answers 401 and 403. The client only sends the ID token; the user's permission keys (never role names, R-SEC-03) come from the API (`permissions` of `GET /api/v1/admin/me`) and are used only to hide menus and buttons — the API still enforces them. A password change revokes every refresh token (DEC-094): the web signs the user in again. In development the client connects to the Firebase Auth Emulator.

## Security headers

`src/lib/security-headers.ts` builds the headers and both CSPs (DEC-104 (10), DEC-113). `next.config` sets on every response `Strict-Transport-Security` (without `includeSubDomains` until the production domain is chosen), `Referrer-Policy: strict-origin-when-cross-origin` and `X-Content-Type-Options: nosniff`. Each document response carries one `Content-Security-Policy`, which allows only this site, the API and the domains of Firebase Authentication and Storage (and the emulator when it is configured), with `frame-ancestors 'none'`; the two CSPs differ only in `script-src`:

- `/admin` and `/pos`: `src/proxy.ts` makes a new nonce for each request (`crypto.randomUUID()` in base64) and sets the same CSP on the request, where Next.js reads the nonce and puts it on its framework scripts, page bundles and inline scripts, and on the response. `script-src` is `'self' 'nonce-…' 'strict-dynamic'` (and `'unsafe-eval'` in development only), with no `'unsafe-inline'`, so an injected inline script does not run. The pages render at each request (*Rendering*). The router's prefetches (`next-router-prefetch`) skip the proxy and carry no CSP: they are RSC payloads, never shown as a page; a browser prefetch of a document (`purpose` or `sec-purpose: prefetch`) goes through the proxy and gets the CSP with a nonce, since the browser may show it as the page. A URL under `/admin` that matches no page reaches the catch-all page of the admin layout, which calls `notFound()`: its 404 renders at the request inside the layout, with the nonce (DEC-114 (2)). Its status is 200, since the admin layout renders its pages in the browser only once the session is known, so the server never meets the `notFound()`; the page's own metadata gives the title. Under `/pos`, until its first screen brings the same catch-all, such a URL gets the 404 that Next.js prerenders, whose scripts carry no nonce and are blocked: its text still shows. A third-party script added later is loaded by a script that has the nonce (`'strict-dynamic'`) or by `<Script nonce>`, with the nonce parsed from the `content-security-policy` request header (`headers()` of `next/headers`); there is no `x-nonce` header.
- Every other path (the storefront later): `next.config` sets a static CSP, whose `script-src` holds `'unsafe-inline'` (and `'unsafe-eval'` in development only), since static pages carry no nonce; the storefront deploy Decision settles it (`docs/STATUS.md`). The `headers()` source leaves out `/admin` and `/pos` case-sensitively, as the proxy matcher does, so `/ADMIN` keeps the static CSP.

`style-src` keeps `'self' 'unsafe-inline'` on both: Radix sets style attributes, which a nonce does not cover. The ID token lives in the browser storage of the Firebase SDK, so the CSP is the main guard against script injection.

## Environment variables

| Variable | Content |
|---|---|
| `NEXT_PUBLIC_API_BASE_URL` | Base URL of `ZenGear.Api`, without a path: the paths of the contract start with `/api/v1/admin` for the admin and POS parts and `/api/v1/shop` for the storefront (DEC-106) |
| `NEXT_PUBLIC_FIREBASE_*` | Firebase web app config (public by design) |
| `NEXT_PUBLIC_FIREBASE_AUTH_EMULATOR_HOST` | Development and CI only: the Auth Emulator (`127.0.0.1:9099`) |

A Zod schema (`src/env-schema.ts`) checks these variables; the root layout imports the checked values (`src/env.ts`), so a missing or malformed one fails `next build` when it prerenders the pages (`next typegen` needs none). Outside a production build, the values of the development environment (`docs/ACCEPTANCE.md`: the API on the launch profile `http`, the emulator, project `demo-zengear`) are the defaults, so `npm run dev` needs no env file; a value set in the environment wins. Values per environment are set in Vercel and, for CI, in `.github/workflows/pipeline.yml`; no env file is committed.

## Packages

npm with `web/package-lock.json` committed; `web/.npmrc` sets `save-exact`, `engine-strict` (the `engines` range of `package.json`, the major of `.nvmrc`) and `ignore-scripts`, which build, tests and screen flows run with (DEC-104 (4)). Use the npm bundled with that Node.js, as CI does (`setup-node`): the lockfile of web-shell was written with it, and a newer npm installed globally may write it differently (not measured). A package is added only when `web/approved-packages.json` lists it (`web/CLAUDE.md`). The packages that official shadcn items need (`date-fns`, `react-resizable-panels`, the four `@dnd-kit` packages) are listed by DEC-116 (6) and installed only when a screen uses an item that needs them; `date-fns` serves only the calendar and the date pickers (with the locale `vi` of `date-fns/locale`), and dates shown anywhere else stay `Intl.DateTimeFormat` in Asia/Ho_Chi_Minh, without the time zones of date-fns.

## Tests and checks

Vitest with Testing Library (jsdom; Vite compiles TSX itself, so no React plugin) for the Zod schemas, the mapping of Problem Details onto form fields, and the hooks; Playwright for the screen flows, run by `/finish-feature` (step 7) against the development environment and in the CI job `acceptance` against the API image and the Firebase Auth Emulator, with screenshots for the owner's test guide (DEC-100). ESLint (`eslint-config-next`), Prettier and `next typegen` + `tsc --noEmit` run locally (ESLint on each edited file and the typecheck at the end of a Claude Code turn, through the hooks of DEC-104) and in the CI job `web-test`, with the build, `npm run gen:api` compared with the committed `schema.d.ts` and the keys of the message files. Business rules are tested in the API (`pr_gates` coverage counts API tests only).

## Screens

Screen list, per-screen fields and flows: `docs/SCREENS.md` (written before web work starts).
