# ZenGear Web

Next.js with TypeScript, Tailwind CSS, shadcn/ui, TanStack Query, Zustand, React Hook Form + Zod — hosted on Vercel (Root Directory `web`). Read `../docs/FRONTEND.md` before adding code. All commands below run from `web/`.

## Rules that must not be broken

- API types come only from `src/lib/api/schema.d.ts`, generated from `../api/openapi/ZenGear.Api.json` with openapi-typescript. Never edit it and never hand-write a type the API already describes. After any API change: regenerate, fix type errors, commit both together.
- All HTTP calls go through the one typed client in `src/lib/api/` (openapi-fetch). It adds the Firebase ID token, `If-Match` for edits and `Idempotency-Key` for checkout and payments (`../docs/API_CONVENTIONS.md`).
- Server data lives in TanStack Query; Zustand only for client state (cart drawer, POS screen state). Never copy server data into Zustand.
- The API decides prices, totals, stock, permissions and status transitions. Show what the API returns; Zod schemas only check input shape before sending.
- Errors are Problem Details: show `detail` to the user; map `errors` of a validation problem onto the form fields; `ruleId` is for logs, not for display.
- Money is integer VND, formatted with `Intl.NumberFormat('vi-VN')`; dates are shown in Asia/Ho_Chi_Minh.
- UI labels use the Vietnamese terms of `../docs/GLOSSARY.md`; code uses its English entity names.
- Hiding a button is not authorization. Admin and POS pages still rely on the API returning 401/403.
- No secrets in code or in `NEXT_PUBLIC_*` variables; only the public Firebase web config may be public.

## Commands

- Dev server: `npm run dev`
- Regenerate API types: `npm run gen:api`
- Checks before commit: `npm run lint` · `npm run build`
