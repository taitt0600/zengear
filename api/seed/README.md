# Seed data

Reference data needed before the first login. Run in file order after the schema migrations and `db/zengear_08_roles.sql`, and before starting the API, as the schema owner; every file is re-runnable (DEC-095).

| File | Content |
|---|---|
| `01_roles.sql` | 9 roles (actors A01–A11): system roles Admin and Customer, 7 default staff roles; permissions come from code (DEC-095) |
| `02_walk_in_customer.sql` | System account "Khách lẻ" (R-USR-08) |
| `03_tax.sql` | 3 business lines and their VAT-on-revenue rates (DEC-029) |
| `04_warehouses.sql` | MAIN, WARRANTY, DISPLAY, OUTLET |
| `05_cash_book.sql` | Cash fund and system cash book categories |
| `06_adjustment_reasons.sql` | DAMAGED, LOST, MISCOUNT |
| `07_categories_brands.sql` | Categories with `attribute_schema` and brands, from `category_attribute_schemas.json` |
| `08_business_location.sql` | Template for the store location — fill in before running |

Not seeded here: countries, provinces and wards (loaded from the official list of Quyết định 19/2025/QĐ-TTg), the default bank account, expense categories and the first Admin account (linked to its Firebase user at first sign-in).
