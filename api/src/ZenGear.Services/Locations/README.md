# Locations

## Purpose

The store's business locations: name, address, contact and opening hours (LOC-01), which the website, receipts,
emails and the stock transfers of LOC-02 use. It is an upstream area (DEC-106): others read it, and it depends on no
other area. The tables this area owns are listed in `docs/ARCHITECTURE.md`, *Business areas*.

## Modules

- `BusinessLocations` — the staff screen that reads and changes a business location, and the shop's read of the
  public fields (LOC-01).

## Public contract

None: no other area calls a service of Locations yet.

## Dependencies

- `Common` (clock, paging).
- Reads the `Geography` entities (provinces and wards of an address); writes none of them.
- No other area (`ZenGear.ArchitectureTests` checks it).
