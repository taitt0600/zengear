# Screens

The screens of the web app (`web/`), per feature: route, who opens it, what it shows, what it sends to the API and
how each answer of the API is shown. Structure, stack and conventions are in `docs/FRONTEND.md`; this folder says
*what* each screen does, `docs/FRONTEND.md` says *how* screens are built. `/implement-feature` builds the web part of
a feature only when this folder describes its screens (step 7), and only once the screen's row below says nothing
blocks it and its **Layout** is approved (DEC-119); for a feature already done, it runs again as a web-only run that
builds only those screens (step 1).

`docs/SCREENS.md`, named in DEC-110, DEC-119 and earlier rows of `docs/blueprint/decisions.md`, is this folder
since pull request #143; the Decisions keep the old path as the record of their time.

A screen is written here before its feature is built on the web, from the API that exists: every field names the
property of the OpenAPI contract (`api/openapi/ZenGear.Api.json`) it shows or sends. The API decides; the web checks
only the shape of the input (required, length, format) with the limits of its file, which mirror the validators of
the API.

| File | Content |
|---|---|
| `README.md` | This page: the screen list, by part of the web app, and how a screen gets built |
| [`conventions.md`](conventions.md) | Conventions for every screen, the form of a **Layout** included |
| [`gaps.md`](gaps.md) | Known gaps: what the API does not offer yet for the screens; a gap keeps its number once closed |
| `SCR-<area>-<nn>.md` | One screen: what it shows and sends, how each answer of the API is shown, then its **Layout** once approved |

`tools/screen_docs.py` checks that the screen list, the screen files and the gap numbers agree (CI job
`tools-test`), and `tools/feature_context.py` adds the files of a feature's screens, with
`conventions.md` and the gaps they name, to its dossier.

## Screen list

### Admin

| ID | Screen | Route | Feature | Permission | Blocked by |
|---|---|---|---|---|---|
| [SCR-ADM-01](SCR-ADM-01.md) | Admin layout and menu | `/admin` (layout) | AUT-04, STF-09 | signed-in staff | |
| [SCR-AUT-01](SCR-AUT-01.md) | Staff sign-in | `/admin/sign-in` | AUT-01 | — | |
| [SCR-AUT-02](SCR-AUT-02.md) | Required password change | `/admin/change-password` | STF-03 | signed-in staff | |
| [SCR-ACC-01](SCR-ACC-01.md) | My account | `/admin/account` | STF-03 | signed-in staff | |
| [SCR-STF-01](SCR-STF-01.md) | Staff list | `/admin/staff` | STF-05, STF-06, STF-07, STF-08, STF-09 | `staff.read`; actions `staff.disable`, `staff.export`, `staff.approve` | |
| [SCR-STF-02](SCR-STF-02.md) | New staff account | `/admin/staff/new` | STF-01, STF-09 | `staff.create`; the notice without `staff.approve` | |
| [SCR-STF-03](SCR-STF-03.md) | Staff account | `/admin/staff/[id]` | STF-01, STF-02, STF-09, STF-10, STF-14 | `staff.read`; actions `staff.disable`, `staff.approve`, `staff.create` (withdraw), `staff.anonymize`, `staff.pii.read`, `staff.pii.update` | |
| [SCR-LOC-01](SCR-LOC-01.md) | Business location | `/admin/settings/business-location` | LOC-01 | `business_location.update` | gap 3 |

The order of DEC-111 (7), which split the one tooling pull request of DEC-104: the API pull request for the web (gaps 1
and 4) comes before or beside the three tooling pull requests T1 (package gate), T2 (CI and hooks) and T3 (Claude Code
tools), which the owner opens and merges; the web shell (`web/` set up with SCR-ADM-01) came after T1 and T2, and no
screen was built before it. Every admin screen sits inside SCR-ADM-01. The POS and
the storefront have no screen yet: their features (catalog, stock, orders) have no API.

The web shell is built (SCR-ADM-01 and SCR-AUT-01): a screen whose *Blocked by* cell is empty and whose **Layout**
is approved can be built with its feature; without a Layout, the ready check of `/implement-feature` holds it as a
cell would (DEC-119 (1)). Until then its route opens a short placeholder page, "Màn hình này đang được xây dựng."
inside the layout (SCR-LOC-01), so no menu link leads to a 404; the feature replaces it. The screens
built before DEC-119 — SCR-ADM-01, SCR-AUT-01, SCR-AUT-02 and SCR-ACC-01 — need a Layout only when their layout
changes. A `refactor(web)` pull request that changes the layout of a screen (the pull request (B) of DEC-118) needs
that screen's approved Layout first and builds it; a new Layout of a screen `web/` already has (not a placeholder
page) is built the same way, by a `refactor(web)` pull request of a local session when it only changes the layout,
or by a `feat(web): … (<Feature ID>)` pull request on `feat/<id>-web` when it adds behaviour (DEC-123), which a Next row of
`docs/STATUS.md` keeps in view: `/design-screen` adds that row in a `docs/STATUS.md` pull request beside the Layout
pull request, unless a Next row already names that pull request (`shell-refactor` for SCR-ADM-01 and SCR-AUT-01). A
date picker uses the shadcn calendar with date-fns (DEC-116 (6)).
