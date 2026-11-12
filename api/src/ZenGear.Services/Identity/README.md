# Identity

## Purpose

Who may use the system and what they may do: user accounts (staff, and customers from AUT-02), signing in, the
access of each request, roles and permissions. The staff and customer profiles belong to the `User` aggregate, so
they stay here (DEC-106). The tables this area owns are listed in `docs/ARCHITECTURE.md`, *Business areas*.

## Modules

- `Access` — signing in (AUT-01) and the access of each request: the user's status and permissions (AUT-04,
  R-SEC-04), read through the Redis access cache of the mechanism folder `AccessCache` (R-SEC-07).
- `Account` — my account: read and change my own profile and password (STF-03).
- `StaffAccounts` — staff accounts: create (STF-01), disable and restore (STF-02), list (STF-05), each within the
  caller's manageable set (R-SEC-12), and the first Admin of `bootstrap-admin` (DEC-096).
- `Permissions` — the permission catalog of the code and its sync to the database at startup (DEC-095); the default
  manageable sets come from the seed, never from the sync (R-SEC-12, DEC-129).
- `Roles` — the roles of the Admin: create, change the permissions, discount and manageable set (Phạm vi quản lý,
  R-SEC-12), delete, and the permission list (AUT-05).

Planned (DEC-106): `CustomerAccounts` with AUT-02.

## Public contract

None: no other area calls a service of Identity yet.

## Dependencies

- `Common` (clock, business rule errors, paging, advisory lock keys).
- External systems through `ZenGear.Data/Externals`: `IAuthProvider` (Firebase Authentication) and the Redis
  connection.
- No other area.
