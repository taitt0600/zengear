# ZenGear Web

Next.js with TypeScript, Tailwind CSS, shadcn/ui, TanStack Query, Zustand, React Hook Form + Zod, next-intl, Firebase JS SDK — hosted on Vercel (Root Directory `web`). Read `../docs/FRONTEND.md` before adding code and `../docs/SCREENS.md` for the screens. All commands below run from `web/`. Decision: DEC-104.

## Rules that must not be broken

- API types come only from `src/lib/api/schema.d.ts`, generated from `../api/openapi/ZenGear.Api.json` with openapi-typescript. Never edit it and never hand-write a type the API already describes. After any API change: regenerate, fix type errors, commit both together.
- All HTTP calls go through the one typed client in `src/lib/api/` (openapi-fetch; the instance is `src/lib/api/api.ts`). It adds the Firebase ID token to every call but those of `/api/v1/shop/`; the caller passes `If-Match` for edits (`etagOf` of `src/lib/api/concurrency.ts`, a typed header parameter of the contract) and `Idempotency-Key` for checkout and payments (`newIdempotencyKey`) (`../docs/API_CONVENTIONS.md`).
- Server data lives in TanStack Query; Zustand only for client state (cart drawer, POS screen state). Never copy server data into Zustand.
- The API decides prices, totals, stock, permissions and status transitions. Show what the API returns; Zod schemas only check input shape before sending.
- Errors are Problem Details: the message shown comes from the problem's `type`, mapped to a key of the message files, with `detail` only as the fallback; map `errors` of a validation problem onto the form fields; `ruleId` is for logs, not for display.
- Money is integer VND, formatted with `Intl.NumberFormat`; dates are shown in Asia/Ho_Chi_Minh with `Intl.DateTimeFormat` (no date library).
- Every text the user sees goes through next-intl (`t('key')`) from `messages/vi.json`, never written in JSX; the labels of business terms are the Vietnamese terms of `../docs/GLOSSARY.md`; code uses its English entity names. Phase 1 ships Vietnamese only; English comes later for the storefront only.
- `src/components/ui/` holds the shadcn/ui components exactly as `npm exec --no -- shadcn add` wrote them (the CLI pinned in `package.json`, DEC-111 (2); never `npx shadcn@latest`): never edit them by hand, and never `add --overwrite` while working on a feature (an update of a component is its own `chore` pull request). After an `add`, run `python tools/shadcn_lock.py write` from the repository root. Theme them with the CSS variables of `src/app/globals.css` and `components.json`; a new variant or behaviour is a wrapper in `src/components/app/` or `src/features/<area>/`. `src/components/ui/.shadcn-lock.json` holds their hashes; once the DEC-104 tooling is in, CI fails when a file differs.
- A package is added only when `approved-packages.json` lists it, with an exact version (DEC-104, DEC-111 (3); `tools/pr_gates.py` checks both and the lockfile). A package that an official installer of an approved package adds (`shadcn add` of an official item, the install command in the documentation of an approved package) and the list does not hold is approved first by a pull request of its own that the owner merges (DEC-111 (5); `/implement-feature`, step 4); any other package needs a Decision first. The shadcn CLI may add the packages of an official component that the list already holds.
- A shadcn component that writes English text a user reads or hears (the screen-reader text of `sidebar`, the close button of `sheet`, the labels of `sonner`'s toasts) is not used, or that text is turned off and replaced by one from the message files (DEC-104 (6)); ESLint does not check `src/components/ui/`.
- Icons are named imports from `lucide-react`; animation uses `motion` with `MotionConfig reducedMotion="user"`, and stays minimal in the admin and the POS.
- Hiding a button is not authorization. Admin and POS pages still rely on the API returning 401/403; the client layout redirects a visitor without a session, and `src/proxy.ts` only makes fixed route redirects.
- No secrets in code or in `NEXT_PUBLIC_*` variables; only the public Firebase web config may be public. `src/env-schema.ts` checks them (Zod); outside a production build the values of the development environment are its defaults, so no env file is needed for `npm run dev`.

## Commands

Node.js is the version of `.nvmrc` (`npm ci` refuses a Node.js outside the `engines` range of `package.json`, `engine-strict`); use the npm bundled with it, and run `npm ci` again whenever `package-lock.json` changed. The scripts call the same commands as the CI job `web-test`, which runs the binaries through `npm exec --no --`.

- Dev server: `npm run dev` (http://localhost:3000/admin, after `tools/dev_reset.ps1` and the API on the launch profile `http`)
- Regenerate API types: `npm run gen:api`
- Checks before commit: `npm run lint` (`eslint .`) · `npm run format` (`prettier --check .`; fix with `npm exec --no -- prettier --write <files>`) · `npm run typecheck` (`next typegen && tsc --noEmit`) · `npm run test` (`vitest run`) · `npm run build` (`next build`, which needs the `NEXT_PUBLIC_*` values)
- Screen flows: `npm run e2e` (Playwright, `e2e/`) against the development environment (`../docs/ACCEPTANCE.md`); CI runs them in the job `acceptance` against the API image and the Firebase Auth Emulator
