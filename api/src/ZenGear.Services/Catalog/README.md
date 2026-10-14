# Catalog

## Purpose

What the store sells and how it is organised: categories, brands, series, products and their SKUs, images, badges,
tags and PC build components. The tables this area owns are listed in `docs/ARCHITECTURE.md`, *Business areas*.

## Modules

- `Brands` — the brands for the staff: list, read, create, rename, retire and restore (PRD-02); none is ever deleted
  (R-DEL-01).

## Public contract

None: no other area calls a service of Catalog yet.

## Dependencies

- `Common` (clock, business rule errors, paging).
- No other area.
