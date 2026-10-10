# Coding guide

How to write and judge the backend code of ZenGear, for Claude Code and for the owner coding by hand (offline, or
without access to Claude). `docs/ARCHITECTURE.md` says *where* code goes and which rules the architecture tests
enforce; this guide says *how* to write it and how to choose when principles pull in different directions.
Decision: DEC-106.

> **Status** (DEC-106 is being rolled out): sections 1 to 4 apply now, and section 7 except question 6. Sections 5
> and 6 describe the target layout; until the pull requests below are merged, the code and `docs/ARCHITECTURE.md`
> still follow DEC-097 (`ZenGear.Services/<Area>/Models/`, `Validators/`, `business_locations` in Inventory,
> `*Public*` names, routes without prefix).
>
> | Pull request (DEC-106, Consequences) | Brings |
> |---|---|
> | 1. Documentation | this guide |
> | 2. Owner path | `docs/ARCHITECTURE.md`, `docs/API_CONVENTIONS.md` (URLs, Authentication), `api/CLAUDE.md`, the architecture tests, the review questions in `plan-reviewer` and `code-reviewer`, step 7 of `/implement-feature`, `tools/structure_metrics.py` at module level |
> | 3. Refactor A | `<Area>/<Module>/` folders and namespaces (`git mv`), area `Locations`, the README of each area |
> | 4. Refactor B | `/api/v1/admin/` and `/api/v1/shop/` added by a convention, the fallback policy, `[ShopEndpoint]`, `*Public*` renamed `*Shop*`, `IUserAccessReader` without its interface |
>
> The READMEs of the areas come with refactor A rather than with this guide: the areas and modules they describe
> exist only after it. Pull requests 2 and 3 may be one pull request, because the architecture tests check the
> folders they move.

## 1. Order of priority

SOLID, DRY, YAGNI and KISS help each other most of the time. When they pull apart, decide in this order:

1. **Correct and safe.** The specification (`docs/blueprint/`, `docs/DATABASE.md`, `docs/API_CONVENTIONS.md`) and
   the security rules come first. A neat design that leaks a cost field or skips a rule is wrong.
2. **KISS and YAGNI.** The simplest code that does today's job. No abstraction, option or extension point for a
   need that is not in a feature yet. Code you did not write has no bugs and costs nothing to read.
3. **DRY, by the rule of three.** Two copies are fine. Merge at the third, when the shared shape is known. Merging
   too early couples things that change for different reasons, which costs more than the copy.
4. **SOLID, when there is a real need.** Single responsibility at the level of a service per family of use cases
   (`StaffService`, `StaffStatusService`), not one class per method. Dependency inversion only where section 2
   allows an interface.

Two questions settle most cases: **how many callers does this have today**, and **what does waiting cost**? If the
answer is "one" and "nothing", write the simple version.

## 2. When an interface exists

An interface exists only for one of these reasons; otherwise the class is used directly:

| Reason | Today |
|---|---|
| A test replaces it with a fake (test seam) | `IPermissionCatalog` (a test catalog in `PermissionSyncTests`), `IAuthProvider` (`FakeAuthProvider`) |
| It is an external system, behind `ZenGear.Data/Externals/<System>` | `IAuthProvider` (Firebase) |
| Another area calls it: the public contract of an area | Listed in the area's README; none yet |
| It has two implementations | — |

These four reasons are those of DEC-106; a new reason needs a Decision. An interface with none of them is removed
(DEC-106 removes `IUserAccessReader`). The audit of DEC-106 kept two interfaces by name: `IClock`, the one clock of
every area (R-TZ-01; tests fake the `TimeProvider` under it), and `IAccessCache`, the access cache of R-SEC-07 over
Redis.

## 3. Intentional duplication

These look like duplication but must stay separate. Do not merge them for DRY:

| Duplication | Why it stays |
|---|---|
| A DTO per audience (`BusinessLocationDto` for staff, `BusinessLocationPublicDto` for the shop) | R-SEC-10: one shared type is one permission bug away from showing cost fields or internal notes to customers |
| A list DTO and a detail DTO of one entity (`StaffListItemDto`, `StaffDto`) | The list carries less personal data (no work phone) and less work per row |
| The error code of each rule in its validator | Every 400 names its own `ruleId` (`docs/API_CONVENTIONS.md`, Errors) |
| One Postman folder per feature | Each feature is tested alone, in any order |
| Migrations | A migration that has run is never edited |

## 4. Comments

- A comment says **why**, never **what**: the code says what.
- Traceability to the specification lives in **test names** (`R_USR_13_...`) and in the Decisions, not in comments.
  A public method may name its rule once (`/// <summary>Staff accounts, one page (R-USR-13).</summary>`); it does
  not restate the rule.
- Keep a comment where the code cannot explain itself: an ordering that keeps pages stable, a transaction boundary,
  a lock, a workaround and its reason.
- A comment that copies the specification is cut (DEC-106 measured 974 comment lines, 25 % of the code).

## 5. Structure at a glance (target of DEC-106)

The rules are in `docs/ARCHITECTURE.md` once pull request 2 of the Status table is merged; this is the map to find code.

```
ZenGear.Api/Controllers/<Area>/                 one controller per service, named after it
ZenGear.Services/<Area>/<Module>/               the module, flat: service, request, DTO, validator, query, job
ZenGear.Services/<Area>/<Module>/<Mechanism>/   only for a mechanism of five types or more (Identity/Access/AccessCache)
ZenGear.Services/<Area>/README.md               purpose, modules, public contract, dependencies of the area
ZenGear.Data/Entities/<Area>/                   entities, shared by the modules of the area
ZenGear.Data/Configurations/<Area>/             their EF Core configurations
tests: ZenGear.Services.Tests/<Area>/<Module>/, ZenGear.Api.IntegrationTests/<Area>/<Module>/
```

- **Area** owns tables (`docs/ARCHITECTURE.md`, Business areas). A service writes only its area's tables; it may read
  any table; to change another area's data it calls that area's public service.
- **Module** is one aggregate or one group of screens around it, inside an area. A module folder exists even when
  the area has one module.
- **Names** put the entity first and say what the type is: `StaffCreateRequest`, `StaffDto`,
  `StaffCreateRequestValidator`, `StaffService`, `StaffStatusService`, `StaffController`.

### Areas and modules of DEC-106

| Area | Tables | Modules |
|---|---|---|
| `Identity` | `users`, `customer_profiles`, `staff_profiles`, `roles`, `permissions`, `user_roles`, `role_permissions` (the profiles belong to the `User` aggregate) | `Access` (sign-in, the access of a request, `AccessCache`), `Account` (my account), `StaffAccounts`, `Roles` (AUT-05), `Permissions`; `CustomerAccounts` from AUT-02 |
| `Locations` | `business_locations` | `BusinessLocations`. Upstream: it reads `Geography` and depends on no other area |
| `Customers` | `addresses`, `wishlists` | added with their features |
| `Staff` | — | created only when staff business tables come (STF-04); it reads the profiles through Identity |

The other areas keep the tables of `docs/ARCHITECTURE.md`. An area grows into its own project when it passes 8
modules or 60 files, when the build and tests of the solution pass 5 minutes, or when a second developer works on it
alone (DEC-106); `tools/structure_metrics.py` will report the first (pull request 2 of the Status table).

### URLs and controllers

| Audience | Prefix | Controller | Service | DTO |
|---|---|---|---|---|
| Staff screens and POS | `/api/v1/admin/` | `<Entity>Controller`, `<Entity><UseCase>Controller` | `<Entity>Service`, `<Entity><UseCase>Service` | `<Entity>Dto` |
| Customer channel (shop) | `/api/v1/shop/` | `<Entity>ShopController` | `<Entity>ShopService` | `<Entity>ShopDto` |

- A controller declares only its resource (`[Route("staff")]`); the prefix is added from its name by a convention.
- Every action declares its access. Without it, the fallback policy requires a signed-in user. A shop controller
  carries `[ShopEndpoint]` (anonymous, with the shop's rate limit and cache policies), and no other controller does;
  an architecture test checks both. `/health` stays outside both prefixes, with `AllowAnonymous`.
- One controller per service: a new family of use cases on a resource gets its own service and controller on the
  same route (`StaffStatusController` beside `StaffController`). Sonar S6960, which flags a controller whose actions
  use disjoint dependencies, catches most breaches.

## 6. Adding a feature by hand

The steps `/implement-feature` follows, written for a person. Each command says where it runs. The example is
STF-05 (the staff list); paths are those of section 5.

1. **Read what to build** (repository root): `python tools/feature_context.py STF-05 --out out/feature-STF-05.md`
   gives the feature row, its rules, use cases, decisions, glossary rows and tables in one file. A rule that is
   unclear is a question for a Decision, not a guess.
2. **Branch** (root): `git switch main`, `git pull --ff-only origin main`, `git switch -c feat/stf-05`.
3. **Data, only when the feature adds or changes a table**:
   - The table must already be in `api/db/` and `docs/DATABASE.md`. Changing them is a change of the specification:
     it needs an accepted Decision first, and the pull request becomes an owner path.
   - The entity in `ZenGear.Data/Entities/<Area>/`, its configuration in `Configurations/<Area>/`; a rule on one
     entity is a method of the entity.
   - In `api/`: `dotnet ef migrations add <Name> -p src/ZenGear.Data -s src/ZenGear.Api`, then the schema comparison
     of `api/CLAUDE.md` (the CI job `schema` runs it too).
4. **Tests first**, named after the rule they prove:
   - unit tests of validators and entity methods in `ZenGear.Services.Tests/<Area>/<Module>/`;
   - endpoint, transaction and constraint tests in `ZenGear.Api.IntegrationTests/<Area>/<Module>/`, deriving from
     `IntegrationTestBase` (PostgreSQL and Redis in containers; `TestUsers`, `TestRoles` insert test data);
   - example: `R_USR_13_lists_staff_accounts_only_with_their_list_fields`;
   - in `api/`: `dotnet test ZenGear.slnx --filter "FullyQualifiedName~StaffList"` and see them fail for the
     expected reason.
5. **Service layer**, in `ZenGear.Services/<Area>/<Module>/`:
   - the request (`StaffListRequest`) and the DTO (`StaffListItemDto`), records;
   - the validator (`StaffListRequestValidator`, FluentValidation), each failure with `.WithErrorCode("R-…")` when it
     breaks a rule, and a message that never repeats the value sent;
   - the method on the service of the use-case family (`StaffService.ListAsync`, taking a `CancellationToken`):
     validate, query with `ZenGearDbContext` (projection to the DTO, `AsNoTracking` for reads), and for a rule
     broken by the current data throw `BusinessRuleException(ruleId, problemType, title, detail)`, which the API
     returns as a 409.
6. **Controller**, in `ZenGear.Api/Controllers/<Area>/`: one action that calls the service, with
   - its access attribute (`[RequirePermission(PermissionKeys.StaffRead)]`);
   - `[ProducesResponseType]` for every answer and an XML `<response>` comment for each error code, which feed the
     OpenAPI document;
   - `[BadRequestRule(<Validator>.RuleId)]` when the 400 of model binding (`?roleId=abc`) must name the rule, as
     STF-05 does;
   - no data access and no rule of its own.
7. **Postman** (root): a folder named after the feature ID in `postman/collections/ZenGear/`, following
   `.claude/skills/api-collection/SKILL.md`: `.resources/definition.yaml`, one `NN <name>.request.yaml` per scenario
   with its tests, the `X-ZenGear-As` header to sign in, `{{run}}` for values that must be unique, run state only in
   `pm.variables` (ids read during the run), and a dev-only fixture for state the API cannot create.
8. **Check**:
   - in `api/`: `dotnet build ZenGear.slnx` (it refreshes `api/openapi/ZenGear.Api.json`; commit it),
     `dotnet test ZenGear.slnx`, and the drift check
     `dotnet ef migrations has-pending-model-changes -p src/ZenGear.Data -s src/ZenGear.Api`;
   - the environment: `docker compose up -d postgres redis firebase` in `api/`, then from the root
     `pwsh -NoProfile -File tools/dev_reset.ps1`, then the API:
     `dotnet run --project api/src/ZenGear.Api --launch-profile http`;
   - the folder (root):
     `postman collection run postman/collections/ZenGear -e postman/environments/dev.environment.yaml -i STF-05 --no-report-events`,
     then the same without `-i STF-05` for the whole collection.
9. **Commit and pull request**: Conventional Commits with `Refs: STF-05, R-USR-13, DEC-105`; title
   `feat(api): add the staff list (STF-05)`; merge with Rebase and merge once the required checks are green. The job
   `pr-title` checks the title; the PR gates check that every rule of the feature has a test, that a change of the
   specification, a package or build code names an accepted Decision, and the author.

**Offline**: build, unit tests, integration tests and the local API work without a network once everything is on the
machine; pushing and the CI wait for the network. Before going offline, once:
- in `api/`: `dotnet tool restore` (it brings `dotnet-ef`), `dotnet restore ZenGear.slnx`, and
  `dotnet test ZenGear.slnx` (Testcontainers pulls `postgres:18`, `redis:7.4-alpine` and its own helper image);
- in `api/`: `docker compose up -d postgres redis firebase`, which builds the Firebase Auth Emulator image from
  `api/acceptance/firebase` (it downloads its base image and `firebase-tools`);
- the Postman CLI 1.69.0 installed (`postman --version`).

## 7. Review questions

A person reviewing by hand asks these questions now; `plan-reviewer` and `code-reviewer` ask them from pull request
2 of the Status table on:

1. Does it do what the specification says, and nothing it does not?
2. **Is there a simpler way** that does the same job?
3. Does every interface, base class or option have a reason from section 2, or a second caller today?
4. Is this duplication intentional (section 3)? If not, is it the third copy?
5. Does every comment say why, not what?
6. Is the code in its area and module, and does it change another area's data only through that area's public
   services? (From refactor A on, when areas and modules exist.)

A finding of questions 2 to 4 is labelled `over-engineering` or `duplication` in the report, so that the numbers can
be followed over time.
