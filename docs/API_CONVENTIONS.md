# API conventions

REST over HTTPS, JSON, served by `ZenGear.Api`.

## Contract

The OpenAPI document `api/openapi/ZenGear.Api.json` is generated from the controllers at build time and committed. The web app generates its TypeScript types from it (`docs/FRONTEND.md`). Every endpoint declares its response types and Problem Details responses so the document is complete.

## URLs

- Base path `/api/v1`. A breaking change gets a new version prefix.
- Plural resource nouns in kebab-case: `/api/v1/products`, `/api/v1/purchase-orders/{id}/receipts`.
- Actions that are not CRUD are sub-resources named after the business command: `POST /api/v1/orders/{id}/confirm`, `POST /api/v1/shifts/{id}/close`.
- No personal data in URLs or query strings (phone, email); send it in the body.

## Requests and responses

- JSON properties in camelCase; enums and status values as their string values (`"Pending"`, `"CASH"`).
- Timestamps in ISO 8601 UTC (`2026-09-29T09:30:00Z`); dates without time as `yyyy-MM-dd` in Vietnam time.
- Money as integer VND (`15990000`), never floating point.
- Ids are UUID strings.
- Responses are DTOs only; fields a caller may not see are omitted, not nulled (R-SEC-10).

## Status codes

| Code | When |
|---|---|
| 200 / 201 / 204 | Success; 201 with `Location` for a created resource |
| 400 | Validation failed: the request itself is wrong, whatever the data holds. This includes a value a business rule forbids on its own (a password the policy refuses, R-USR-11; a closed holiday with hours, R-LOC-01): the response then carries that rule's `ruleId` |
| 401 / 403 | Missing or invalid Firebase ID token / permission missing |
| 404 | Resource does not exist or is outside the caller's scope (a customer asking for another customer's order) |
| 405 | The resource has no such operation: no endpoint exists for that method on that path (no `POST` or `DELETE` on the business location, R-LOC-01) |
| 409 | Business rule or state conflict: the request is well formed, but the current data refuses it (order already confirmed, stock not available, the last ACTIVE Admin); a concurrent change to the row when the request sent no `If-Match` |
| 412 | `If-Match` does not match the current version |
| 428 | `PUT` / `PATCH` on a resource that has an `ETag` sent without `If-Match` |
| 422 | Not used — a rule broken by the request alone is 400, a rule broken by the current data is 409 |

## Errors

Problem Details (RFC 9457) with `application/problem+json`, built with ASP.NET Core `AddProblemDetails`:

```json
{
  "type": "/problems/stock-not-available",
  "title": "Stock not available",
  "status": 409,
  "detail": "SKU L-1 has 1 unit available, 2 requested.",
  "ruleId": "R-INV-02",
  "traceId": "00-4bf92f…"
}
```

Validation errors use `ValidationProblemDetails` with an `errors` object keyed by camelCase field name. `ruleId` names the business rule that was violated when there is one, on a 400, a 403 or a 409 alike.

## Paging, sorting, filtering

- `?page=1&pageSize=20` (max 100); response `{ "items": [...], "page": 1, "pageSize": 20, "totalCount": 134 }`.
- `?sort=createdAt` or `?sort=-createdAt` (descending); only documented fields.
- Filters as plain query parameters (`?status=Pending&from=2026-09-01`).

## Concurrency and idempotency

- Resources that can be edited return an `ETag` built from `xmin`; updates send `If-Match` and get 412 when the row changed (optimistic concurrency).
- `PUT` / `PATCH` on a resource that has an `ETag` without `If-Match` gets 428.
- A concurrent change detected on save (`xmin`) for a request without `If-Match` (a command such as `POST /orders/{id}/confirm`) gets 409 with `type` `/problems/concurrency-conflict` and no `ruleId`; the caller reloads and retries.
- `POST` requests that must not run twice (checkout, payments) send an `Idempotency-Key` header; a repeated key returns the first result (R-ORD-12). Payment callbacks are idempotent by the gateway transaction id (R-TX-07).

## Authentication

`Authorization: Bearer <Firebase ID token>` on every request except public storefront endpoints and payment callbacks. Payment callbacks (VNPay IPN) are verified by the gateway signature instead.

Every controller action declares its access, checked by a test (AUT-04):

- `[RequirePermission("resource.action")]`: the permission must be granted through one of the caller's roles, never checked by role name (R-SEC-03).
- `[AnyRegisteredUser]`: any registered, ACTIVE user.
- `[AllowAnonymous]`: public.

A token for no ZenGear account gets 403 `/problems/account-not-registered`. A registered user without the permission gets 403 `/problems/permission-denied` with `ruleId` `R-SEC-03`. The scope of the permission (NONE = all data, SELF = the caller's own data) is read with `HttpContext.GetUserAccess()`.

Security-sensitive operations (changing the password) need a recent sign-in: when the `auth_time` of the ID token is more than 5 minutes old the API returns 403 with `type` `/problems/recent-login-required`; the web signs the user in again with Firebase and retries (DEC-094).

## Health

`GET /health` is outside `/api/v1`, anonymous and not in the OpenAPI document. It checks the database connection and returns `text/plain` `Healthy` or `Unhealthy` with 200 or 503; it never returns error details.
