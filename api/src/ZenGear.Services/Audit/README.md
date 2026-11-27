# Audit

## Purpose

The history that every area leaves behind without writing it itself: the field-level changes of `audit_logs`
(R-AUD-01, DEC-086), and later the status changes of `status_history` (R-HIS-01) and the evidence files of
`attachments` (R-ATT-01), each written in the transaction of the change it records. The tables this area owns are
listed in `docs/ARCHITECTURE.md`, *Business areas*.

## Modules

- `AuditLogs` — the EF Core interceptor that writes the `audit_logs` rows of every `SaveChanges` (INSERT, UPDATE of
  the changed columns, DELETE) with the actor, the transaction and the correlation id of the request (R-AUD-01).

## Public contract

None: no other area calls a service of Audit. The interceptor is registered for every DbContext, and the API sets the
actor of the request on `AuditActor`.

## Dependencies

- No other area: the interceptor reads the change tracker of every entity through the EF Core model only.
