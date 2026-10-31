# Frontend

Structure of the ZenGear web app in `web/`. Stack: sheet `Tech Stack` of the blueprint. Decisions: DEC-093, DEC-104, DEC-118.

## One app, three parts

One Next.js app (App Router) serves the storefront, the admin pages and the POS, separated by route groups so each part has its own layout:

```
web/src/
├─ app/                    routes only: parameters, guard, a feature's layout and sections composed; no logic
│  ├─ (shop)/              storefront pages: home, category, product, cart, checkout, account (API /api/v1/shop/)
│  ├─ (admin)/admin/       back office: catalog, inventory, purchasing, orders, after-sales, cash book, reports
│  └─ (pos)/pos/           counter sales and shifts, full-screen layout
├─ features/<area>/<module>/   one module of a business area (the area and module names of the backend, DEC-106)
│  ├─ api/                 composes the generated calls, keys and hooks for the module (invalidation, mapping)
│  ├─ components/          the layouts, sections and presentational components of its screens, tests next to them
│  ├─ hooks/               the module's own hooks (form logic, state)
│  ├─ utils/               the module's pure functions
│  ├─ schemas.ts           its Zod schemas
│  └─ index.ts             its public API: the only file another module or app/ imports
├─ features/shell/<module>/  the frame of the admin and of the POS (sidebar, header, guard, no-permission page), same rules
├─ components/ui/          shadcn/ui components, code of the app (open code, DEC-116)
├─ components/app/         the app's own shared components (compositions of components/ui, files of blocks)
├─ hooks/                  hooks two or more modules use (alias @/hooks)
├─ lib/api/                generated/ (Orval: calls, query keys, hooks, models), api.ts and client.ts (the mutator), problems.ts (errors → fields)
├─ lib/auth/               Firebase Authentication client, current user, ID token
├─ lib/utils.ts            cn() of shadcn
├─ utils/                  pure functions two or more modules use: VND money, dates in Asia/Ho_Chi_Minh (created with the first one)
├─ i18n/                   config.ts (the locales) and request.ts (the next-intl request config)
├─ test/                   setup.ts of Vitest and render.tsx (the app's providers for a test)
├─ env-schema.ts, env.ts   the NEXT_PUBLIC_* schema (Zod) and its checked values
└─ proxy.ts                fixed route redirects (/ → /admin until the storefront) and the CSP with a nonce of /admin and /pos; never the session, never authorization
web/messages/vi.json       every text the user sees, by key (en.json later, storefront only)
web/e2e/                   Playwright screen flows against the API and the Firebase Auth Emulator
```

`features/` follows the business areas and modules of `docs/ARCHITECTURE.md` and the READMEs of `api/src/ZenGear.Services` (`identity/account`, `identity/staff-accounts`, `locations/business-locations`, …) so a feature ID maps to the same area on both sides. A module holds only the subfolders it needs, and nothing sits directly in `features/<area>/`. A module that other modules import (such as `identity/access`, the staff session) exports no screen, since its index would carry that screen into their pages (DEC-121). That is the one case where a web module splits a backend module: the sign-in (SCR-AUT-01) belongs to the backend's `Access`, but on the web it is the module `identity/sign-in` (inside `identity/access` it added 54 KB of uncompressed First Load JS to every page of the admin shell, measured on the first build of the pull request (A) of DEC-118).

## Modules and boundaries

Code starts in its module and moves to a shared folder (`components/app`, `hooks`, `utils`, `lib`) only once two or more modules use it (DEC-118 (1)). ESLint refuses, with `import/no-restricted-paths` of `web/eslint.config.mjs` (tested by `src/lint.test.ts`), the imports DEC-118 (2) rules out:

- `app/` imports a feature module only through its `index.ts`;
- a feature never imports `app/`;
- a module imports another module only through its `index.ts` (its own files by relative paths, a convention ESLint does not check);
- the shared folders and the root files of `src/` import neither `features/` nor `app/`.

The modules and the shared entries are read from the disk, so a new module or folder is covered without editing the config. The rule judges the file an import resolves to, and the `@/…` imports resolve only when ESLint runs from `web/`, as `npm run lint`, CI, the eslint-check hook and Vitest do; an editor's ESLint started at the repository root may miss a boundary. A `vi.mock` path is not an import: a test may mock a file inside another module when that module's code imports it.

## Screens in three layers

Each screen is built in three layers (DEC-118 (3)), so that a change of layout touches one file:

- `<screen>-layout.tsx` places the sections through slots (grid, spacing, order, what shows at each screen size); it holds no data and no business state;
- a section is one block of the screen's approved design (DEC-119), or of the current screen when it has none; it gets its data through hooks and holds the behavior;
- a presentational component renders its props only.

The page in `app/` composes the layout with the sections; when one piece of client state feeds several sections (the staff session of SCR-AUT-02), a section provides it to the others through a context. The tests of sections and presentational components find elements by role and label, never by the structure of the DOM. ESLint's `max-lines` warns above 150 lines of code per file (blank lines and comments not counted), except tests, `components/ui` and `lib/api/generated/`; a warning fails neither lint nor CI, and `code-reviewer` checks that a new file, or a file whose content changed, has none.

## Rendering

- Storefront catalog and product pages are rendered on the server so search engines see them; they call only shop endpoints (`/api/v1/shop/`).
- Admin and POS pages sit behind sign-in: the page and the layout of its screen are server components, and the sections are client components, shown once the session is known (by the client layout of the part, or by a section that holds the session, as on SCR-AUT-02); they call endpoints with the Firebase ID token. The root layout of `/admin` (and of `/pos` when it comes) awaits `connection()` (`next/server`), so every page of `/admin` and `/pos` renders on the server at each request and gets the nonce of its CSP (DEC-113 (3)): they are not cached by a CDN and cannot be exported statically, so the web runs on `next start` or a platform with a server (Vercel).
- `url_redirects` (old slugs, 301) are resolved by the API; the storefront follows what the API returns.

## Data from the API

- Client: `npm run gen:api` runs Orval (`web/orval.config.ts`, DEC-122 (4)) on `../api/openapi/ZenGear.Api.json` and writes `src/lib/api/generated/`: one folder per tag of the contract (`me/me.ts`, `staff/staff.ts`) with the request function, query key and TanStack Query hook of each operation, named from its method and path (`getApiV1AdminMe`, `getGetApiV1AdminMeQueryKey`, `useGetApiV1AdminMe`), and the models in `model/`. Import the tag file and `model`, not the folder's root `index.ts`. The folder changes only through `gen:api`; CI regenerates it and fails when the committed folder differs.
- Calls: every generated call goes through the mutator `apiFetch` of `src/lib/api/api.ts` (built by `createApiFetch` of `src/lib/api/client.ts`): it adds the base URL and the ID token (never sent to `/api/v1/shop/`), passes the `RequestInit` through (`signal`, and later `next` for the fetch cache of a server component), resolves a 2xx as `{ data, status, headers }` and throws any other answer as an `ApiError` with its Problem Details (DEC-117), typed per operation (`ErrorType`, the 400 with the enum of its codes). Two helpers of `src/lib/api/concurrency.ts`: `If-Match` from the `ETag` of the loaded resource (`etagOf(headers)`), passed as the typed header parameter of the edit, and a new `Idempotency-Key` per checkout or payment attempt (`newIdempotencyKey`, kept while that attempt is retried), passed in the `headers` of the call's `RequestInit` (the last argument of a request function, `request` of a hook), which the generated code merges with its own.
- Every list endpoint returns `PagedDto<T>` (`items`, `page`, `pageSize`, `totalCount`).
- Server state: the generated hooks and query keys; `features/<area>/<module>/api/` only composes them (DEC-122 (4)): it invalidates the affected queries after a command (`confirm`, `close shift`) instead of editing the cache by hand, and chooses a behaviour by the problem `type`. A GET whose answer the module maps (the user with its ETag of `meQuery`) is read only through that module, never through its generated hook, which would share the key with another shape of data. A mutation whose variables must not stay in the mutation cache (a password) calls the request function, not the generated mutation hook. A query whose own reader handles its failures, as the admin layout does for GET /me, sets `meta: { reportErrors: false }`, so the query cache does not report them to the layout's rules.
- Paging: `page`, `pageSize`, `sort` as in `docs/API_CONVENTIONS.md`, kept in the URL search params so a list can be shared and reloaded.

## Forms and errors

- React Hook Form with a Zod schema per form; the schema checks input shape only (required, format, length) and mirrors the column limits in `docs/DATABASE.md`.
- The web shows the API's texts and decides nothing about an error (DEC-117 (4)): the `detail` of each item of `errors` goes under the field whose name its `pointer` (`#/name`) or `parameter` (`roleId`) gives, and an item no field shows goes in an alert at the top of the form (`fieldErrors` of `lib/api/problems.ts`, `components/app/form-problems.tsx`); any other problem shows its `detail` in a toast or on the page (`problemText`). The web never maps a `type`, a `code` or a status to a text of its own; `ruleId` is for logs, never shown.
- For an answer of the API, `messages/vi.json` holds only two texts of the web itself: `problems.network` (no answer) and `problems.unknown` (an answer that cannot be read, or a 500, which has no `detail`). Errors of Firebase Authentication happen in the browser and never reach the API: they are mapped by the `auth/*` codes Firebase publishes. Zod checks only the shape of the input before sending (DEC-104) and gives no text to an error the API answers.
- `type` and `code` only choose a behaviour: a 412 or 428 shows its `detail` and reloads the record; 401 signs the user out; a 403 by its `type` (`/problems/permission-denied` shows a no-permission page, `/problems/recent-login-required` signs the user in again and retries (DEC-094), the others as the Conventions of `docs/SCREENS.md` say).

## Text and languages

Every text the user sees goes through next-intl (without locale routing), except the texts of the errors the API answers, shown as they come (DEC-117 (4)): `t('key')` with the keys of `web/messages/vi.json`, never a string written in JSX or in an attribute the user reads (`alt`, `aria-label`, `placeholder`, `title`, `label`…): ESLint refuses one, in `components/ui/` too (`react/jsx-no-literals` and `no-restricted-syntax`, tested by `src/lint.test.ts`); a text that the package of a shadcn component shows (sonner, react-day-picker, cmdk, vaul) is set through its prop or locale from the message files, which `code-reviewer` checks since ESLint cannot see it (DEC-116 (2)); and a wrong key fails the typecheck (`src/global.d.ts`). CI checks that every message file has the same keys (`tools/message_keys.py`), so a storefront-only `en.json` needs a Decision on how the message files are split before it comes. Labels of business terms are the Vietnamese terms of `docs/GLOSSARY.md`. Phase 1 ships Vietnamese only. The locales are written in one place, `src/i18n/config.ts` (`locales`, `defaultLocale`): `src/i18n/request.ts` takes the locale and its message file from it, the `Locale` of next-intl's `AppConfig` in `src/global.d.ts` is its type, and the root layout sets `<html lang>` from `getLocale()` (DEC-118 (4)). Money and dates are formatted with the locale of next-intl (`Intl` with that locale; dates in Asia/Ho_Chi_Minh). English comes later for the storefront only: its pages under a `[locale]` segment with `localePrefix: 'as-needed'` (`/en/...` with `hreflang`, `en.json`, an English label column in `docs/GLOSSARY.md`); the admin and the POS have no `[locale]` and stay Vietnamese. Business content in two languages (product names, descriptions, categories, slugs) needs its own Decision; invoices and printed documents stay Vietnamese (DEC-104).

## Components and styling

`components/ui/` holds the shadcn/ui components as code of the app (open code, DEC-116 (1)): added with `npm exec --no -- shadcn add` (the CLI pinned exactly in `web/package.json`, DEC-111 (2); `--no` fails instead of downloading), then edited like other code, also in a feature pull request; in a pull request with an `add`, the first commit holds only what `add` wrote and the edits come after, so that git keeps the registry's version. The style is `new-york` on Radix (`components.json`, written by hand in web-shell, since the `init` of shadcn 4.22 offers only other styles; its registry item `index` then installed the theme and the packages of DEC-111 (1)). Every item of the official registry `@shadcn` may be used, also one that writes English (the `sidebar`, the close button of `sheet`): in the pull request of its `add`, its texts go through `t(key)` of the namespace `ui`; an item that needs `@base-ui/react` or `@shadcn/react` is not used, and a third-party registry needs a Decision of its own (DEC-116 (2), (7)). The font is the system font stack, which covers Vietnamese without a download. The design of shadcn is kept: the classes that set the look of a component (colour, spacing, radius, font size, shadow) change only to fix accessibility; the look changes through the CSS variables of `app/globals.css` (`:root`, `@theme inline`) and `components.json`, and a new variant uses the tokens of the theme. A component in `components/app/` or `features/<area>/<module>/` composes several of them. A block of `@shadcn` (`sidebar-*`, `login-*`, `dashboard-01`…) is a template: its files move to `features/<area>/<module>/` or `components/app/`, without its sample page, data and texts, and are never updated again from the registry (DEC-116 (3)). An update of an installed component is a `chore(web)` pull request of its own: `add --diff` against the registry, `add --overwrite` only when none of the files it would write was edited, otherwise the registry's changes applied by hand (DEC-116 (4); `components/ui/` stays out of Prettier so that the diff stays small). Icons are named imports from `lucide-react`; a block that imports another icon set is switched to them. Animation uses `motion` (`MotionConfig reducedMotion="user"` in the root layout, `LazyMotion`), minimal in the admin and the POS. Accessibility target: WCAG 2.2 AA.

## Authentication

Firebase Authentication in the browser (Phase 1): staff sign in to admin and POS; customers sign in on the storefront (Phase 2). The SDK keeps the session (its default persistence); there is no session cookie. Admin and POS pages sit behind sign-in, with their sections in the browser: the client layout of each part (on SCR-AUT-02, which sits outside it, the section that holds the session, `ChangePasswordSession`) sends a visitor without a Firebase session to the sign-in page (the session lives in browser storage, which `proxy.ts` on the server cannot see). `proxy.ts` makes fixed route redirects and sets the CSP of `/admin` and `/pos`, and never reads the session; neither is authorization, and the API still answers 401 and 403. The client only sends the ID token; the user's permission keys (never role names, R-SEC-03) come from the API (`permissions` of `GET /api/v1/admin/me`) and are used only to hide menus and buttons — the API still enforces them. A password change revokes every refresh token (DEC-094): the web signs the user in again. In development the client connects to the Firebase Auth Emulator.

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

npm with `web/package-lock.json` committed; `web/.npmrc` sets `save-exact`, `engine-strict` (the `engines` range of `package.json`, the major of `.nvmrc`) and `ignore-scripts`, which build, tests and screen flows run with (DEC-104 (4)). Use the npm bundled with that Node.js, as CI does (`setup-node`): the lockfile is written with it, and a newer npm installed globally may write it differently (not measured). The `overrides` of `web/package.json` (an owner path; gate 7 of `tools/pr_gates.py` does not read them, so the owner checks them in review; never `npm audit fix`) hold only an exact version of a vulnerable transitive package whose parent has no fix yet, removed when the parent declares a fixed version (DEC-122 (5)). A package is added only when `web/approved-packages.json` lists it (`web/CLAUDE.md`). The packages that official shadcn items need (`date-fns`, `react-resizable-panels`, the four `@dnd-kit` packages) are listed by DEC-116 (6) and installed only when a screen uses an item that needs them; `date-fns` serves only the calendar and the date pickers (with the locale `vi` of `date-fns/locale`), and dates shown anywhere else stay `Intl.DateTimeFormat` in Asia/Ho_Chi_Minh, without the time zones of date-fns.

## Tests and checks

Vitest with Testing Library (jsdom; Vite compiles TSX itself, so no React plugin; setup and the providers of a test in `src/test/`) for the Zod schemas, the mapping of Problem Details onto form fields, and the hooks; a test whose code loads a generated module of `src/lib/api/generated/` mocks `@/lib/api/api` (the mutator, which reads the Firebase session when it loads), and a component test mocks its module's `api/` instead; Playwright for the screen flows, run by `/finish-feature` (step 7) against the development environment and in the CI job `acceptance` against the API image and the Firebase Auth Emulator, with screenshots for the owner's test guide (DEC-100). ESLint (`eslint-config-next`), Prettier and `next typegen` + `tsc --noEmit` run locally (ESLint on each edited file and the typecheck at the end of a Claude Code turn, through the hooks of DEC-104) and in the CI job `web-test`, with the build, `npm run gen:api` compared with the committed `src/lib/api/generated/` and the keys of the message files. Business rules are tested in the API (`pr_gates` coverage counts API tests only).

## Screens

Screen list, per-screen fields and flows: `docs/SCREENS.md` (written before web work starts).
