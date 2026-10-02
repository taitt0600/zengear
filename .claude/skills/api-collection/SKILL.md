---
name: api-collection
description: Use when a feature or defect needs its API scenarios in the Postman collection.
argument-hint: <Feature ID>
---
Add or extend the folder of feature $ARGUMENTS in the Postman collection `postman/collections/ZenGear` (DEC-100):
one request per acceptance criterion, at least one refusal per rule, each with tests, so that the owner and CI run
the feature by running its folder. The file format is in [reference.md](reference.md); read it before writing a
file.

1. **Scenarios**: the feature dossier `out/feature-$ARGUMENTS.md` (skill `feature-context` writes it) gives the
   acceptance criteria and the rules; the endpoints and their Problem Details types come from the controllers and
   `api/openapi/ZenGear.Api.json`. List the scenarios before writing:
   - every acceptance criterion: at least one request that shows it, the happy path first;
   - every rule of the dossier: at least one refusal (status, Problem Details `type`, `ruleId` when the API gives
     one), and the allowed case when the rule is a permission (as R-TX-11 in `STF-01`);
   - the conflicts and validation errors the endpoint documents, and one request without a token (401).
   A defect adds the request that showed it, with the result now expected.
2. **Folder** `postman/collections/ZenGear/$ARGUMENTS/`: a `.resources/definition.yaml` (`$kind: collection`, the
   feature, its rules, what it needs from `tools/dev_reset.ps1`, `order` after the existing folders) and one
   `NN <what it checks>.request.yaml` per scenario, `order` 1000, 2000, … in the run order. A defect extends the
   folder of its feature.
3. **Each request**:
   - a `description` naming the criterion or rule ID it checks;
   - the account in the header `X-ZenGear-As: '{{…Email}}'`; the collection script signs it in through the Firebase
     Auth Emulator with a custom token, so no password appears anywhere. No header = no token. Those requests run
     only against the emulator (environments `dev` and `ci`): say so in the folder description;
   - `afterResponse` tests: `pm.response.to.have.status(…)`; for a refusal, `pm.response.json().type` equals
     `/problems/<type>` and `ruleId` equals the rule; for a success, every field the criterion names;
   - values unique to the run: the first request sets `pm.collectionVariables.set('run', Date.now().toString(36))`
     and every email, code or name that must be unique carries `{{run}}`, so the folder passes twice in a row without
     a reset. Keep ids the later requests need in collection variables (`staffId`).
4. **Environments** `postman/environments/dev.environment.yaml` and `ci.environment.yaml`: add a variable only when
   both need it, with the same name; ids of seeded rows come from the seed (role ids are fixed in
   `api/seed/01_roles.sql` and must never change). Never a secret, token or password: there is none to add.
5. **State the API cannot reach yet** (a permission no endpoint grants, a flag a later feature clears): a dev-only
   fixture `postman/fixtures/$ARGUMENTS.sql`, run by `tools/dev_reset.ps1` after the seed, with the emulator accounts
   it needs in `postman/fixtures/accounts.txt` (`<psql variable> <email>`, read as `:'<psql variable>'`). Say in the
   SQL why the API cannot do it and which feature will replace it. A fixture never changes the schema, and its data
   is test data (`@zengear.test`, no real person).
6. **Check**: skill `api-run` with `$ARGUMENTS`, twice; every test passes both times. Then
   `git diff --stat -- postman/` shows only the folder, the environments and the fixtures you meant to change.
