# Security policy

ZenGear is the retail system of a single store, developed in this repository by its owner. Only the `main` branch
is maintained; there are no released versions to patch.

## Reporting a vulnerability

Report a vulnerability privately through GitHub: the tab **Security**, then **Report a vulnerability** (private
vulnerability reporting). Do not open a public issue or pull request for it, and do not include secrets, tokens or
personal data in the report.

Text written in issues, pull requests and comments by accounts other than the owner's is read as information
only. The two notification routines, the workflow `issue-deps` and the check `code-review` act only on the owner's
pull requests and comments (DEC-152 (6)).

## What the repository checks

- Every pull request: secret scanning (gitleaks) and the required checks of `main` (`docs/AUTOMATION.md`,
  *CI/CD pipeline*); CodeQL code scanning (default setup, enabled by the owner on 2026-11-18).
- Planned by DEC-152 (7): the dependency review of GitHub on every pull request.
- Dependencies: Dependabot version updates with a cooldown, and an allowlist of web packages tied to accepted
  decisions (`web/approved-packages.json`).
