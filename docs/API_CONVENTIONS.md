# API conventions

REST over HTTPS, JSON, served by `ZenGear.Api`.

## Contract

The OpenAPI document `api/openapi/ZenGear.Api.json` is generated from the controllers at build time and committed. The web app generates its TypeScript types from it (`docs/FRONTEND.md`). Every endpoint declares its response types and Problem Details responses so the document is complete. What attributes cannot say is added by the transformers of `OpenApiSetup`: the `ETag` response header of every 200 and 201 whose body is a versioned resource (a DTO with a `[JsonIgnore] uint Version`), beside the `If-Match` request header of its updates, and the `enum` values of a query parameter that a validator restricts (`status` and `sort` of `GET /api/v1/admin/staff`), kept as strings in C# so that a wrong value still gets the 400 of its rule; and the enum of the error codes of each operation (*Errors*).

## URLs

- Every route is under one of two prefixes (DEC-106): `/api/v1/admin/` for the staff screens and the POS, `/api/v1/shop/` for the customer channel (anonymous visitors, and signed-in customers later). `/health` stays outside both.
- A controller declares only its resource (`[Route("staff")]`); the convention `AudienceRoutePrefixConvention` adds `/api/v1/shop/` to a `*ShopController` and `/api/v1/admin/` to every other controller. A controller without `[Route]`, a controller route starting with `api/`, `/` or `~/`, or an action route starting with `/` or `~/` fails the build and the tests.
- Plural resource nouns in kebab-case: `/api/v1/shop/products`, `/api/v1/admin/purchase-orders/{id}/receipts`.
- Actions that are not CRUD are sub-resources named after the business command: `POST /api/v1/admin/orders/{id}/confirm`, `POST /api/v1/admin/shifts/{id}/close`.
- No personal data in URLs or query strings (phone, email); send it in the body.
- Infrastructure rules (CDN cache, rate limit, WAF) allow-list `/api/v1/shop/*`; they are never written as "everything except `/api/v1/admin/`", so a route that misses its prefix is never cached or opened by mistake.
- A breaking change gets a new version prefix. Moving every route under the two prefixes stayed in `v1` (DEC-106, DEC-107 (4)): its only callers were the Postman collection and the tests, changed with it.

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
| 405 | The resource has no such operation: no endpoint exists for that method on that path (no `POST` or `DELETE` on `/api/v1/admin/business-locations`, R-LOC-01) |
| 409 | Business rule or state conflict: the request is well formed, but the current data refuses it (order already confirmed, stock not available, the last ACTIVE Admin); a concurrent change to the row when the request sent no `If-Match` |
| 412 | `If-Match` does not match the current version |
| 428 | `PUT` / `PATCH` on a resource that has an `ETag` sent without `If-Match` |
| 422 | Not used — a rule broken by the request alone is 400, a rule broken by the current data is 409 |

## Errors

Every error is Problem Details (RFC 9457) with `application/problem+json`, built with ASP.NET Core
`AddProblemDetails` (DEC-117 (1)):

- `type`: a relative path `/problems/<kebab-case>`, the stable name of the kind of error; the framework's own answers
  get one too (`/problems/validation` 400, `/problems/unauthenticated` 401, `/problems/forbidden` 403,
  `/problems/not-found` 404, `/problems/method-not-allowed` 405, `/problems/not-acceptable` 406,
  `/problems/precondition-failed` 412, `/problems/unsupported-media-type` 415, `/problems/internal-error` 500,
  `/problems/unexpected-error` for any other status).
- `title` and `detail`: in the language of the request (`Accept-Language`; vi-VN by default and for every language
  without its resources, the only one in Phase 1). They come from the API's resources
  (`ZenGear.Api/Resources/`), keyed by the type, never from a service; they never repeat a value the user sent. A 500
  has no `detail`.
- `status`, `ruleId` when a business rule was violated (on a 400, a 403 or a 409 alike), and `traceId`.
- `errors` on a 400, and on a 409 about one field of the request: an array whose items name where the error is, a
  `pointer` (JSON Pointer to a field of the body, as a URI fragment: `#/newPassword`, `#/regularHours/1/day`) or a
  `parameter` (a query or route parameter: `roleId`), the `code` of the reason and its `detail`. The item of a 409
  has the type as its code (`/problems/email-already-registered` → `EMAIL_ALREADY_REGISTERED`).

```json
{
  "type": "/problems/validation",
  "title": "Dữ liệu không hợp lệ",
  "status": 400,
  "detail": "Vui lòng kiểm tra lại các thông tin đã nhập.",
  "errors": [
    { "pointer": "#/newPassword", "code": "PASSWORD_TOO_COMMON", "detail": "Mật khẩu này quá phổ biến. Vui lòng chọn mật khẩu khác." }
  ],
  "ruleId": "R-USR-11",
  "traceId": "00-4bf92f…"
}
```

A `code` names one reason, in UPPER_SNAKE_CASE `<SUBJECT>_<REASON>` (`PASSWORD_TOO_COMMON`, `NAME_TOO_LONG`),
matching `^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$` and at most 63 characters (DEC-117 (2)). A code is stable: renaming or
removing one is a breaking change. Each operation declares in the OpenAPI document the codes it can answer, as the
enum `<Controller><Action>ErrorCode` of its 400 and `…ConflictCode` of its field-bound 409 (`[ErrorCodes]` on the
action; model binding adds `BODY_REQUIRED`, `BODY_INVALID`, `VALUE_REQUIRED` and `VALUE_INVALID` to every 400).
401, 403, 412, 428 and the other 409s are told apart by their `type` alone, with no code. A service sets only codes
and the values a text needs (`WithErrorCode`, a placeholder such as `{MaxLength}`; `BusinessRuleException` carries
the type, the rule and the field): a new error adds one code or type and one text in the resources, and a new
language adds its resource file and its culture (`LocalizationSetup`).

## Paging, sorting, filtering

- `?page=1&pageSize=20` (max 100); response `{ "items": [...], "page": 1, "pageSize": 20, "totalCount": 134 }`.
- `?sort=createdAt` or `?sort=-createdAt` (descending); only documented fields.
- Filters as plain query parameters (`?status=Pending&from=2026-09-01`).

## Concurrency and idempotency

- Resources that can be edited return an `ETag` built from `xmin`; updates send `If-Match` and get 412 `/problems/precondition-failed` when the row changed (optimistic concurrency).
- `PUT` / `PATCH` on a resource that has an `ETag` without `If-Match` gets 428 `/problems/if-match-required`.
- A concurrent change detected on save (`xmin`) for a request without `If-Match` (a command such as `POST /api/v1/admin/orders/{id}/confirm`) gets 409 with `type` `/problems/concurrency-conflict` and no `ruleId`; the caller reloads and retries.
- `POST` requests that must not run twice (checkout, payments) send an `Idempotency-Key` header; a repeated key returns the first result (R-ORD-12). Payment callbacks are idempotent by the gateway transaction id (R-TX-07).

## Authentication

`Authorization: Bearer <Firebase ID token>` on every request except the shop endpoints (`/api/v1/shop/`) and payment callbacks. Payment callbacks (VNPay IPN) are verified by the gateway signature instead.

The fallback and default authorization policies require a registered, ACTIVE user: an endpoint that declares nothing is closed, and an anonymous caller gets 401 on every route but the shop endpoints and the endpoints outside `/api/v1` below, whether the route exists or not. Every controller action declares its access, checked by a test (AUT-04):

- `[RequirePermission("resource.action")]`: the permission must be granted through one of the caller's roles, never checked by role name (R-SEC-03).
- `[AnyRegisteredUser]`: any registered, ACTIVE user.
- `[ShopEndpoint]` on a `*ShopController`: every action of it is anonymous. No other controller carries it, and `[AllowAnonymous]` is not used on the controllers of `ZenGear.Api`; the access-surface tests of `ZenGear.Api.IntegrationTests` check both. `[ShopEndpoint]` holds only the anonymous access: the shop's rate limit and output cache come with their own Decision before the storefront is deployed (DEC-107 (1), which amends DEC-106 (6)).

`/health`, the OpenAPI document and Scalar are minimal-API endpoints outside `/api/v1`, mapped with `AllowAnonymous()`.

`[ShopEndpoint]` is class-level, and a class-level `IAllowAnonymous` wins over any access declared on an action, so a shop controller cannot hold one action that needs a signed-in customer. The first such endpoint (the customer's own account of AUT-02) and the payment callbacks need a Decision that changes `[ShopEndpoint]` and its test before they are written; they do not follow today's rule.

A token for no ZenGear account gets 403 `/problems/account-not-registered`. A registered user without the permission gets 403 `/problems/permission-denied` with `ruleId` `R-SEC-03`. The scope of the permission (NONE = all data, SELF = the caller's own data) is read with `HttpContext.GetUserAccess()`.

Security-sensitive operations (changing the password) need a recent sign-in: when the `auth_time` of the ID token is more than 5 minutes old the API returns 403 with `type` `/problems/recent-login-required`; the web signs the user in again with Firebase and retries (DEC-094).

## CORS

The web calls the API from another origin (DEC-104 (9)). One CORS policy, set up by `CorsSetup` and run before HTTPS redirection and authentication, applies to every route: `/api/v1/admin/`, `/api/v1/shop/`, `/health` and the OpenAPI document, existing or not. A preflight is answered there without a token and never redirected, and every answer to an allowed origin, a 401, 403, 4xx or 500 Problem Details included, carries `Access-Control-Allow-Origin`, so the web reads its body and `ruleId`.

- **Origins**: the list `Cors:AllowedOrigins` (`WebClientCorsOptions`): `http://localhost:3000` in Development (`appsettings.Development.json`), empty in every other environment until the web deploys, so no cross-origin call is allowed there; then set per environment with `Cors__AllowedOrigins__0`, `__1`, …; configuration merges a list by index, so `__0` replaces the first entry of `appsettings`, it does not add one. Each entry is an origin as the browser sends it: `http` or `https`, lower case, without path, trailing slash or default port. `*`, a wildcard host or any other entry stops the API at start-up; `AllowAnyOrigin` is never used. The preview origins of Vercel stay for later, with the rest of DEC-104's deployment.
- **Methods**: `GET`, `POST`, `PUT`, `PATCH`, `DELETE`.
- **Request headers**: `Authorization`, `Content-Type`, `If-Match`, `Idempotency-Key`.
- **Exposed headers**: `ETag` and `Location`, which the web reads from JavaScript.
- **No credentials**: the web sends a Bearer token, never a cookie, so `Access-Control-Allow-Credentials` is never sent.
- **Preflight max age**: 10 minutes.

## Health

`GET /health` is outside `/api/v1`, anonymous and not in the OpenAPI document. It checks the database connection and returns `text/plain` `Healthy` or `Unhealthy` with 200 or 503; it never returns error details.
