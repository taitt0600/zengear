---
description: Implement one blueprint feature end to end (rules → tests → code)
argument-hint: <Feature ID, e.g. POS-01>
---
Implement feature $ARGUMENTS of ZenGear.

1. Read the row of $ARGUMENTS in `docs/blueprint/features.md`, its use cases in `docs/blueprint/use-cases.md`, every rule listed in its Rules column in `docs/blueprint/business-rules.md`, and the related decisions. A rule ID that is not in `business-rules.md` (R-DB-, R-TX-, R-NOT-, R-AUD-, R-SEC-07, R-ORD-12, R-REC-) is an implementation rule: read it in `docs/blueprint/technical-design.md`, section 8.
2. Look up each table it touches in `docs/DATABASE.md` (`#### <table>`), its owning area in `docs/ARCHITECTURE.md`, and the names in `docs/GLOSSARY.md`.
3. Write a short plan: endpoints (per `docs/API_CONVENTIONS.md`), service methods, entity methods, migrations if any, and one or more tests per rule. Stop and ask if a rule is ambiguous or conflicts with the schema.
4. Backend, following `api/CLAUDE.md`:
   - Tests first (`api/tests/ZenGear.Services.Tests` for entity and service logic, `api/tests/ZenGear.Api.IntegrationTests` for transactions, constraints and endpoints), named after rule IDs.
   - Code in the owning area folders of `ZenGear.Data`, `ZenGear.Services`, `ZenGear.Api`; never write another area's tables directly.
   - From `api/`: `dotnet build ZenGear.slnx`, `dotnet test ZenGear.slnx`, the migration drift check; fix until green. The build refreshes `api/openapi/ZenGear.Api.json`.
5. Web, following `web/CLAUDE.md` — only when `docs/SCREENS.md` describes the screens of this feature; otherwise stop after the backend and say so:
   - From `web/`: `npm run gen:api`, then pages and components in the matching `features/<area>`.
   - `npm run lint` and `npm run build`.
6. Report what was built, rules covered, and any design change needed (as a proposed Decision) — do not change the blueprint silently.
