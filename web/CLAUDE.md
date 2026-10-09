# ZenGear Web

Next.js with TypeScript, Tailwind CSS, shadcn/ui, TanStack Query, Zustand, React Hook Form + Zod, next-intl, Firebase JS SDK — hosted on Vercel (Root Directory `web`). Read `../docs/FRONTEND.md` before adding code and `../docs/SCREENS.md` for the screens. All commands below run from `web/`. Decision: DEC-104.

## Rules that must not be broken

- API types come only from `src/lib/api/schema.d.ts`, generated from `../api/openapi/ZenGear.Api.json` with openapi-typescript. Never edit it and never hand-write a type the API already describes. After any API change: regenerate, fix type errors, commit both together.
- All HTTP calls go through the one typed client in `src/lib/api/` (openapi-fetch). It adds the Firebase ID token, `If-Match` for edits and `Idempotency-Key` for checkout and payments (`../docs/API_CONVENTIONS.md`).
- Server data lives in TanStack Query; Zustand only for client state (cart drawer, POS screen state). Never copy server data into Zustand.
- The API decides prices, totals, stock, permissions and status transitions. Show what the API returns; Zod schemas only check input shape before sending.
- Errors are Problem Details: the message shown comes from the problem's `type`, mapped to a key of the message files, with `detail` only as the fallback; map `errors` of a validation problem onto the form fields; `ruleId` is for logs, not for display.
- Money is integer VND, formatted with `Intl.NumberFormat`; dates are shown in Asia/Ho_Chi_Minh with `Intl.DateTimeFormat` (no date library).
- Every text the user sees goes through next-intl (`t('key')`) from `messages/vi.json`, never written in JSX; the labels of business terms are the Vietnamese terms of `../docs/GLOSSARY.md`; code uses its English entity names. Phase 1 ships Vietnamese only; English comes later for the storefront only.
- `src/components/ui/` holds the shadcn/ui components exactly as `npx shadcn@<pinned version> add` wrote them: never edit them by hand, and never `add --overwrite` while working on a feature (an update of a component is its own `chore` pull request). Theme them with the CSS variables of `src/app/globals.css` and `components.json`; a new variant or behaviour is a wrapper in `src/components/app/` or `src/features/<area>/`. `src/components/ui/.shadcn-lock.json` holds their hashes; once the DEC-104 tooling is in, CI fails when a file differs.
- A package is added only when `approved-packages.json` lists it (DEC-104; `tools/pr_gates.py` checks it once the DEC-104 tooling is in); any other package needs a Decision first. The shadcn CLI may add the packages of an official component that the list already holds.
- Icons are named imports from `lucide-react`; animation uses `motion` with `MotionConfig reducedMotion="user"`, and stays minimal in the admin and the POS.
- Hiding a button is not authorization. Admin and POS pages still rely on the API returning 401/403; the client layout redirects a visitor without a session, and `src/proxy.ts` only makes fixed route redirects.
- No secrets in code or in `NEXT_PUBLIC_*` variables; only the public Firebase web config may be public.

## Commands

- Dev server: `npm run dev`
- Regenerate API types: `npm run gen:api`
- Checks before commit: `npm run lint` · `npm run typecheck` · `npm run test` (Vitest) · `npm run build`
- Screen flows: Playwright, run by CI in the job `acceptance` against the API and the Firebase Auth Emulator
