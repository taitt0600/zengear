# Status

The plan of ZenGear: only what cannot be derived from git, GitHub or the specifications (DEC-110). What is in
flight, what is done and how this file changes: `CLAUDE.md`, *Work state*.

## Next

| Key | Item | Who | Source |
|---|---|---|---|
| web-tooling | Tooling pull request of DEC-104 | local, owner path | DEC-104; `docs/SCREENS.md`, *Screen list* |
| web-api | API for the web: gaps 1 and 4 | local | `docs/SCREENS.md`, *Known gaps* |
| web-shell | `web/` set up with SCR-ADM-01, then the screens whose API exists | local | DEC-104; `docs/SCREENS.md`, *Screen list* |

## Waiting for a trigger

| Trigger | Item | Source |
|---|---|---|
| before PRD-01 | Decision: where code shared by the modules of an area goes (`CatalogSlug` and `CatalogNameValidator` in `Catalog/Brands`, for the third module of Catalog) | DEC-109 (2) |
| before PRD-01 | Decision: a shared `If-Match` version check in `Common` (copied in `BusinessLocationService`, `CurrentUserService`, `BrandService` and `ProductSeriesService`; a new `Common` type changes `docs/ARCHITECTURE.md`) | `docs/CODING_GUIDE.md`, section 1 item 3; code-reviewer of the product series pull request |
| before PRD-05 | Decision: TAX-01 first, or PRD-05 creates the tax tables (`products.tax_class_id` is NOT NULL and no migration creates `tax_classes` yet) | `docs/DATABASE.md`, `#### products` |
| before PRD-05 | The update of a product series refuses moving it to another brand while products reference it (UC-013 extension 3a, R-PRD-07); `fk_products_product_series_series_id` will refuse it in the database once `products` is migrated | DEC-108; `docs/blueprint/use-cases.md`, UC-013 |
| before AUT-02 | Decision: change `[ShopEndpoint]` for the endpoints of a signed-in customer | DEC-107 (1) |
| before PAY-02 | Decision: change `[ShopEndpoint]` for the payment callbacks (VNPay IPN) | DEC-107 (1) |
| storefront deploy | Decision: rate limit and output cache of `/api/v1/shop/` | DEC-107 (1) |
| the owner codes a feature by hand (the owner says when) | Hand-coding measure of DEC-106 | DEC-107 (8) |
| end of the DEC-110 measurement | Evaluate DEC-110 against its criteria | DEC-110 (7) |
