# Status

The plan of ZenGear: only what cannot be derived from git, GitHub or the specifications (DEC-110). What is in
flight, what is done and how this file changes: `CLAUDE.md`, *Work state*.

## Next

| Key | Item | Who | Source |
|---|---|---|---|
| web-commands | `/finish-feature` and `/fix-defect` run the checks of `web/` (`npm run lint`, `format`, `typecheck`, `test`, `build`, `e2e`) and start `npm run dev` for the test before merge, then `docs/ACCEPTANCE.md` (*Test a feature*) says so | local, owner path | pull request of web-shell, *Needs decision* 5; `web/CLAUDE.md`, *Commands* |
| csp-nonce | Decision: a nonce in the CSP of `/admin` and `/pos` through `src/proxy.ts` (changes DEC-104 (10) and the rule of `proxy.ts`) | local | DEC-104 (10); `docs/FRONTEND.md`, *Security headers*; pull request of web-shell, *Needs decision* 3 |
| STF-03 | SCR-AUT-02 and SCR-ACC-01 | local | `docs/SCREENS.md`, SCR-AUT-02, SCR-ACC-01 |
| STF-05 | SCR-STF-01, without the role filter (gap 2) | local | `docs/SCREENS.md`, SCR-STF-01, gap 2; DEC-105 |
| STF-02 | SCR-STF-03; its date picker needs a Decision first | local | `docs/SCREENS.md`, SCR-STF-03; DEC-111 |

## Waiting for a trigger

| Trigger | Item | Source |
|---|---|---|
| before PRD-01 | Decision: where code shared by the modules of an area goes (`CatalogSlug` and `CatalogNameValidator` in `Catalog/Brands`, for the third module of Catalog) | DEC-109 (2) |
| before PRD-01 | Decision: a shared `If-Match` version check in `Common` (copied in `BusinessLocationService`, `CurrentUserService`, `BrandService` and `ProductSeriesService`; a new `Common` type changes `docs/ARCHITECTURE.md`) | `docs/CODING_GUIDE.md`, section 1 item 3; code-reviewer of the product series pull request |
| before PRD-05 | (1) Decision: TAX-01 first, or PRD-05 creates the tax tables (`products.tax_class_id` is NOT NULL and no migration creates `tax_classes` yet); (2) the update of a product series refuses moving it to another brand while products reference it (UC-013 extension 3a, R-PRD-07); `fk_products_product_series_series_id` will refuse it in the database once `products` is migrated | (1) `docs/DATABASE.md`, `#### products`; (2) DEC-108; `docs/blueprint/use-cases.md`, UC-013 |
| before AUT-02 | Decision: change `[ShopEndpoint]` for the endpoints of a signed-in customer | DEC-107 (1) |
| before PAY-02 | Decision: change `[ShopEndpoint]` for the payment callbacks (VNPay IPN) | DEC-107 (1) |
| `api/db/zengear_08_roles.sql` applies to the migrated schema (`api/acceptance/init/init.sh` and `tools/dev_reset.ps1` no longer skip it) | Make the integration tests connect as a login granted `zengear_app` and test that UPDATE and DELETE on an append-only table and DELETE on `notifications` are refused (the acceptance environment may switch too); today the integration tests, the acceptance environment and the local set-up connect as `postgres` | DEC-089; `api/acceptance/compose.yml` (`ConnectionStrings__ZenGear`); `api/CLAUDE.md`, *Local database* |
| storefront deploy | Decision: rate limit and output cache of `/api/v1/shop/` | DEC-107 (1) |
| the owner codes a feature by hand (the owner says when) | Hand-coding measure of DEC-106 | DEC-107 (8) |
| end of the DEC-110 measurement | Evaluate DEC-110 against its criteria | DEC-110 (7) |
| the 5th `/implement-feature` or `/fix-defect` run after the pull request that applies DEC-112 merges | (1) Evaluate DEC-112 against its criteria (8), with the Token lines of issue #24; (2) whatever the result, a Decision aligns the wording of DEC-112 (2) with DEC-111 (6), (7) and with web-shell (`security-guidance` on at its end, and its unpinned install of `claude-agent-sdk` accepted by the owner while DEC-111 (6) (d) keeps `playwright` off for unpinned code); (3) evaluate the trial of `security-guidance` (DEC-112 (2)) on the reports of the first 3 web features after web-shell: keep it, or set it to `false` and delete its line of `/finish-feature` step 14; if fewer than 3 have merged at this trigger, this row keeps only item (3), with the trigger "the 3rd web feature after web-shell merges" | (1) DEC-112 (8); `docs/AUTOMATION.md`, *Metrics*; (2) DEC-111 (6), (7); `docs/AUTOMATION.md`, *Tools of local sessions*; pull request #77, *Deviations*; pull request of web-shell; (3) DEC-112 (1), (2); `docs/AUTOMATION.md`, *Tools of local sessions* |
