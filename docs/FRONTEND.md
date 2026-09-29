# Frontend

Structure of the ZenGear web app in `web/`. Stack: sheet `Tech Stack` of the blueprint. Decision: DEC-093.

## One app, three parts

One Next.js app (App Router) serves the storefront, the admin pages and the POS, separated by route groups so each part has its own layout:

```
web/src/
├─ app/
│  ├─ (storefront)/        public pages: home, category, product, cart, checkout, account
│  ├─ (admin)/admin/       back office: catalog, inventory, purchasing, orders, after-sales, cash book, reports
│  └─ (pos)/pos/           counter sales and shifts, full-screen layout
├─ features/<area>/        components, query hooks and Zod schemas per business area (same area names as the backend)
├─ components/ui/          shadcn/ui components
├─ lib/api/                schema.d.ts (generated), client.ts (openapi-fetch), query keys
├─ lib/auth/               Firebase Authentication client, current user, ID token
└─ lib/format/             VND money, dates in Asia/Ho_Chi_Minh
```

`features/` follows the business areas of `docs/ARCHITECTURE.md` (`catalog`, `inventory`, `sales`, `pos`, …) so a feature ID maps to the same area on both sides.

## Rendering

- Storefront catalog and product pages are rendered on the server so search engines see them; they call only public endpoints.
- Admin and POS pages are client components behind sign-in; they call endpoints with the Firebase ID token.
- `url_redirects` (old slugs, 301) are resolved by the API; the storefront follows what the API returns.

## Data from the API

- Types: `npm run gen:api` runs openapi-typescript on `../api/openapi/ZenGear.Api.json` and writes `src/lib/api/schema.d.ts`. CI regenerates it and fails when the committed file differs.
- Calls: one openapi-fetch client in `src/lib/api/client.ts` with the base URL, the ID token, `If-Match` from the `ETag` of the loaded resource, and a new `Idempotency-Key` per checkout or payment attempt (kept while that attempt is retried).
- Server state: TanStack Query hooks per area (`features/sales/queries.ts`); query keys in one place; after a command (`confirm`, `close shift`) invalidate the affected queries instead of editing the cache by hand.
- Paging: `page`, `pageSize`, `sort` as in `docs/API_CONVENTIONS.md`, kept in the URL search params so a list can be shared and reloaded.

## Forms and errors

- React Hook Form with a Zod schema per form; the schema checks input shape only (required, format, length) and mirrors the column limits in `docs/DATABASE.md`.
- A 400 validation problem is mapped onto the form fields by its `errors` keys (camelCase, same as the form fields).
- A 409 shows `detail`; a 412 tells the user the record changed and reloads it; 401 signs the user out; 403 shows a no-permission page.

## Authentication

Firebase Authentication in the browser: staff sign in to admin and POS; customers sign in on the storefront. The client only sends the ID token; roles and permissions come from the API (current user endpoint) and are used to hide menus — the API still enforces them.

## Environment variables

| Variable | Content |
|---|---|
| `NEXT_PUBLIC_API_BASE_URL` | Base URL of `ZenGear.Api` (`…/api/v1` is added by the client) |
| `NEXT_PUBLIC_FIREBASE_*` | Firebase web app config (public by design) |

Values per environment are set in Vercel; `.env.local` for local work is not committed.

## Screens

Screen list, per-screen fields and flows: `docs/SCREENS.md` (written before web work starts).
