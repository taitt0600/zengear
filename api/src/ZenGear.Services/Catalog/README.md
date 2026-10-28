# Catalog

## Purpose

What the store sells and how it is organised: categories, brands, series, products and their SKUs, images, badges,
tags and PC build components. The tables this area owns are listed in `docs/ARCHITECTURE.md`, *Business areas*.

## Modules

- `Brands` — the brands for the staff: list, read, create, rename, retire and restore (PRD-02); none is ever deleted
  (R-DEL-01). It also holds what the other modules of Catalog share for now: `CatalogSlug` (R-PRD-06),
  `CatalogNameValidator` (DEC-109 (2)) and the code `IS_ACTIVE_REQUIRED` of `BrandUpdateRequestValidator` (DEC-117).
- `Series` — the product series for the staff, each of exactly one brand: list (by brand), read, create, rename, move
  to another brand, retire and restore (PRD-03); none is ever deleted (R-DEL-01).

## Public contract

None: no other area calls a service of Catalog yet.

## Dependencies

- `Common` (clock, business rule errors, paging).
- No other area.
