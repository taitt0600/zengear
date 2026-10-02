---
name: api-run
description: Use to run the Postman collection, or one feature's folder, against the local development API.
argument-hint: "[Feature ID]  (no argument: the whole collection)"
---
Run the Postman collection `postman/collections/ZenGear` with the Postman CLI against the API of the current branch
on the development environment (DEC-100, `docs/ACCEPTANCE.md`), and report every failed test. With `$ARGUMENTS`, run
the folder of that feature only.

1. **Reset**: `pwsh -NoProfile -File tools/dev_reset.ps1`. Exit code 1 names a missing prerequisite and how to fix
   it; exit code 2 means something is not local, so stop and report it as is. If it reports that the API answers,
   stop the API this session started (the background `dotnet run` of step 2) and run the reset again; an API you
   did not start, ask the owner to stop. The API synchronizes the permissions when it starts, so it always starts
   after the reset.
2. **API** from the current branch: `dotnet run --project api/src/ZenGear.Api --launch-profile http` in the
   background, then wait until `http://localhost:5256/health` answers `Healthy` (at most two minutes; on a failure,
   read its output and report it). Leave it running when the caller says so (the test before merge of
   `/finish-feature`); otherwise stop it at the end.
3. **Run**:

   ```bash
   postman collection run postman/collections/ZenGear -e postman/environments/dev.environment.yaml \
     -i "$ARGUMENTS" --no-report-events
   ```

   without `-i` for the whole collection. Postman CLI 1.69.0 (`postman --version`); it runs from the files without
   signing in, and prints a notice "No authorization data found" that is not an error. `--no-report-events` keeps the
   results on the machine. Do not add `--output`, `--postman-api-key` or `postman login`: they need a Postman account.
4. **Report**: requests run, tests passed and failed (the summary table), then for each failed test: the folder and
   request, the test name, the expected and the actual value, and the status code with the Problem Details `type` of
   the response, from the CLI output (the run prints each request, its status and each test). A failure in the collection script (sign-in)
   means the emulator, the accounts of `tools/dev_reset.ps1` or a fixture is missing: say which.

Never pass a secret with `--env-var`: nothing the collection needs is secret, and the command line ends up in the
transcript.
