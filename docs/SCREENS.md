# Screens

The screens of the web app (`web/`), per feature: route, who opens it, what it shows, what it sends to the API and
how each answer of the API is shown. Structure, stack and conventions are in `docs/FRONTEND.md`; this file says
*what* each screen does, `docs/FRONTEND.md` says *how* screens are built. `/implement-feature` builds the web part of
a feature only when this file describes its screens (step 7), and only once the screen's row below says nothing
blocks it and its **Layout** is approved (DEC-119); for a feature already done, it runs again as a web-only run that
builds only those screens (step 1).

A screen is written here before its feature is built on the web, from the API that exists: every field names the
property of the OpenAPI contract (`api/openapi/ZenGear.Api.json`) it shows or sends. The API decides; the web checks
only the shape of the input (required, length, format) with the limits below, which mirror the validators of the API.

## Conventions for every screen

- **Text**: every label, message and button comes from `web/messages/vi.json` (next-intl). The texts quoted below
  are the Vietnamese wording for that file; terms of `docs/GLOSSARY.md` keep its Vietnamese label. The text of an
  error the API answers is the API's (`title`, `detail` and the `detail` of each `errors` item, from its vi-VN
  resources, DEC-117): the tables of the screens say only where it shows.
- **Access**: a staff screen needs a signed-in staff account; the permission of each screen is the one its endpoints
  require. Menus and buttons the user's permissions do not allow are hidden (`permissions` of `GET /api/v1/admin/me`,
  the user's own permission keys; never a role name), and the API still answers 403.
- **Errors** (DEC-117 (4)): a 400, or a 409 about one field, puts the `detail` of each `errors` item under the
  form field its `pointer` or `parameter` names and keeps what was typed; an item no field shows goes in an alert at
  the top of the form; any other problem shows its `detail` in a toast, or on the page when the page cannot show
  without it; a 500 or an answer that cannot be read shows "Đã có lỗi xảy ra. Vui lòng thử lại." and no answer
  "Không kết nối được máy chủ. Vui lòng kiểm tra mạng và thử lại." (the web's only texts for an answer of the API);
  `ruleId` is never shown. 401 signs the user out to the sign-in page; 403
  `/problems/permission-denied` shows the no-permission page; 403 `/problems/recent-login-required` asks for the
  password again and retries once (DEC-094); 403 `/problems/password-change-required` (any call while the default
  password is unchanged) goes to SCR-AUT-02; 403 `/problems/account-disabled` (an account disabled while signed in,
  R-SEC-04) and 403 `/problems/account-not-registered` sign the user out to SCR-AUT-01, which shows the problem's
  `detail` once.
- **Edits with an ETag** (`If-Match`): the form keeps the `ETag` of the loaded resource; 412 (or 428) shows its
  `detail` in a toast and reloads the form.
- **Leaving a form**: an admin edit form does not warn before the user leaves it with changes not saved (a menu
  link, a reload, closing the tab); the changes are lost. Chosen in the web-only pull request of STF-03 for every
  admin form alike (SCR-LOC-01 has no warning either), instead of one screen with a warning.
- **States**: loading shows skeleton rows or fields, never an empty screen; an empty list says why it is empty; a
  button that sends a request is disabled until the answer comes, so a double click sends one request.
- **Grouping**, by the shadcn components, never content loose on the page ground: related content sits in a
  `Card` (a form, a list with its filters, a group of details); a page of one card (sign-in, password change) has
  its heading in the card's `CardTitle`, a page of several cards keeps its heading above them. An area with nothing
  to show (an empty list, no permission, not found, a failed load) is an `Empty` with an outline (`border`, dashed):
  an icon in `EmptyMedia variant="icon"`, the message in `EmptyTitle` (the page's h1 on a page that is only this
  Empty), the way out, when there is one, in `EmptyContent`; an empty list's Empty takes the place of the rows inside
  the list's card, so that its filters stay. Chosen by the owner on the Layout of SCR-ADM-01; a screen built before
  (SCR-ACC-01, the placeholder page) follows it with its next Layout.
- **Help on screen**, in every design and its Layout, from the message files like every text:
  - a **tooltip** (`Tooltip`) on every button that shows only an icon, with its `aria-label` (the corner close
    button of `Dialog` and `Sheet` too, with `ui.close`, in the pull request that next changes those components),
    and on every label cut short, with its full text; never the only place of a text a user needs (a tooltip does
    not show on touch); the first tooltip opens after a delay and the next ones at once (`TooltipProvider` with
    Radix's `delayDuration` 700 and `skipDelayDuration` 300, not shadcn's 0);
  - a **description** under the title of each card, dialog, sheet and Empty that says what it is for
    (`CardDescription`, `DialogDescription`, `SheetDescription`, `EmptyDescription`), except an Empty whose title
    is the text of an API problem;
  - a **hint text** (`FieldDescription`, tied to its input with `aria-describedby`) under every field with a
    length, a format or a use to explain; a field that is only required, or must match another field, needs none;
  - a **placeholder** in every field with a format (an email, a phone number, a code with a fixed pattern, an
    amount, a time `HH:mm`): an example value as it would be typed, without `…` (unlike the Web Interface
    Guidelines, since an ellipsis would read as part of the value), in `muted-foreground`, with a reserved example
    domain (`example.com`, RFC 2606) and never a real person or company; never instead of the label nor repeating
    it, and the hint never repeats its example; none in a password field, a field without a format, a read-only
    field, a select or a date picker, except the search box that filters a list, which has one, an example of a
    value it finds (chosen by the owner on the Layout of SCR-STF-01).

  A Layout lists these texts with the screen's other new texts. Chosen by the owner on the Layout of SCR-ADM-01,
  the placeholder on the Layout of SCR-AUT-01; a screen built before (SCR-AUT-01, SCR-AUT-02, SCR-ACC-01) follows
  it with its next Layout.
- **Dates** `yyyy-MM-dd` from the API are shown `dd/MM/yyyy`; timestamps are shown in Asia/Ho_Chi_Minh.
- **Accessibility**: WCAG 2.2 AA; every form works with the keyboard alone, every field has a visible label, errors
  are announced to screen readers.
- **Design system** (DEC-119 (2)): <https://claude.ai/artifact/DAYPEgxSLaHrHYG99Tmntb>, built 2026-10-31 from
  `web/src/app/globals.css` and `web/src/components/ui/` and updated 2026-11-11 to commit 39485c9; `/design-screen`
  brings the system up to date when those files changed since, and then the update's date and commit.
- **Layout** (DEC-119 (3)): the approved design of a screen, a subsection at the end of its section that
  `/design-screen <SCR-ID>` writes and the merge of its Layout pull request approves; a later change of the design is
  a new Layout pull request. The component tree is the part a diff can review, and the runs build from it:

  ````markdown
  ### Layout

  - **Design**: <link>, drawn <yyyy-mm-dd> with the design system of <yyyy-mm-dd>; approved by the merge of the
    pull request that wrote this subsection (DEC-119).
  - **Artboards**: desktop (1440 px), narrow (390 px); states: loading, empty, error, no permission.
    (Drawn in the light theme, with one dark artboard more when the screen has colours outside neutral, such as a
    badge or a destructive button, DEC-123 (5).)
  - **shadcn**: the `shadcn add` of the components and blocks the design needs (the tree names them too,
    `/design-screen` step 2), with where a block's files move (DEC-116 (3)), or "no `add`".
  - **Texts** (`messages/vi.json`), new ones marked *new*: each text of **Help on screen** with its key and where
    it goes (the description of each card, dialog, sheet and Empty, but an Empty whose title is an API problem; the
    hint and placeholder of each field that has them; the tooltip of each icon-only button, its `aria-label`, and
    of each label cut short), with the screen's other new texts.
  - **Component tree** (DEC-118 (3)), under `web/src/features/<area>/<module>/components/`:

    ```text
    <screen>-layout.tsx                slots: <a> | <b> | <c>; narrow: <what changes>
    ├── <screen>-<block>-section.tsx   <what it shows> — <shadcn component or block>; CardDescription <key>;
    │                                  loading: <…>; empty: Empty with EmptyTitle <key> and EmptyDescription <key>
    └── <screen>-<block>-section.tsx   … the field <label> with its FieldDescription <key> and placeholder <key>
        └── <piece>.tsx                presentational — <shadcn component>; its icon-only button in a Tooltip,
                                       its text <key> passed as a prop
    ```
  ````

## Screen list

| ID | Screen | Route | Part | Feature | Permission | Blocked by |
|---|---|---|---|---|---|---|
| SCR-ADM-01 | Admin layout and menu | `/admin` (layout) | Admin | AUT-04 | signed-in staff | |
| SCR-AUT-01 | Staff sign-in | `/admin/sign-in` | Admin | AUT-01 | — | |
| SCR-AUT-02 | Required password change | `/admin/change-password` | Admin | STF-03 | signed-in staff | |
| SCR-ACC-01 | My account | `/admin/account` | Admin | STF-03 | signed-in staff | |
| SCR-STF-01 | Staff list | `/admin/staff` | Admin | STF-05 | `staff.read`; actions `staff.disable` | |
| SCR-STF-02 | New staff account | `/admin/staff/new` | Admin | STF-01 | `staff.create` | gap 2 |
| SCR-STF-03 | Staff account | `/admin/staff/[id]` | Admin | STF-01, STF-02 | `staff.read`; actions `staff.disable` | |
| SCR-LOC-01 | Business location | `/admin/settings/business-location` | Admin | LOC-01 | `business_location.update` | gap 3 |

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

---

## SCR-ADM-01 Admin layout and menu

Every admin screen except the sign-in page sits in this layout.

- **Store name and user**: the store name at the top of the menu, and at its foot the signed-in user's `name` (from
  `GET /api/v1/admin/me`) with a menu: "Tài khoản của tôi" (SCR-ACC-01) and "Đăng xuất" (signs out of Firebase,
  clears the query cache, goes to SCR-AUT-01). The header holds the menu button and where the page sits (**Layout**).
- **Menu**: "Nhân viên" (SCR-STF-01, needs `staff.read`), "Cài đặt" → "Địa điểm kinh doanh" (SCR-LOC-01, needs
  `business_location.update`). An entry the user cannot use is hidden.
- **Guard**: without a Firebase session, every admin route goes to SCR-AUT-01 with the requested path kept as
  `?next=`. When `GET /api/v1/admin/me` says `mustChangePassword: true`, every admin route goes to SCR-AUT-02. When it says
  `isStaff: false`, the user is signed out with "Tài khoản này không phải tài khoản nhân viên."
- **No-permission page** (403): "Bạn không có quyền xem trang này." and a link back to the first menu entry the user
  can open (other than this page), or "Tài khoản của tôi" when there is none. A page also shows it at once when the
  user lacks the permission of its endpoints; a 403 `permission-denied` shows it until the path changes.
- **Other texts**: the skip link "Bỏ qua đến nội dung chính"; on narrow screens the menu opens with "Mở menu" and
  closes with "Đóng menu"; the menu's name is "Menu"; when GET `/me` fails otherwise, the problem's `detail` (or
  the web's text of *Conventions*) with "Thử lại" and "Đăng xuất". The menu shows "ZenGear" instead of the store
  name until gap 6 is closed.
- **Not found**: an `/admin` URL that matches no screen shows "Không tìm thấy trang này." inside the layout, with the
  link "Về trang chính" (`/admin`, the landing page); signed out, it goes to SCR-AUT-01 like any admin route.
- **Toasts** ("Đã lưu." and the problems that *Errors* above sends to a toast) sit in a region named "Thông báo".
- **Theme** (DEC-123): "Giao diện" offers "Sáng", "Tối" and "Theo hệ thống", the system theme by default; the choice
  stays in the browser (`localStorage`), never sent to the API. From md up it sits in the header; below md in the
  user menu.

### Layout

- **Design**: <https://claude.ai/artifact/LMwdwkWoxsS3hFQwxE4cXA>, drawn 2026-11-01 with the design system of
  2026-11-01; approved by the merge of the pull request that wrote this subsection (DEC-119). The shadcn block
  `sidebar-07` (a menu that collapses to icons, DEC-118 (5)); the page area is left empty, since every other screen
  draws its own, except on the no-permission page and the 404, which this design draws. Changed 2026-11-05 for the
  theme switch of DEC-123, drawn with the design system of 2026-11-05 (its dark artboards use the softer `.dark`
  values of DEC-124: background `oklch(0.235 0 0)`, card, popover and sidebar `0.255`, secondary, muted, accent and
  sidebar-accent `0.335`, the rest as `app/globals.css`; `tokens.json` gains a dark theme only with DEC-123 (5)),
  approved by the merge of the pull request of that change.
- **Artboards**: desktop (1440 px) with the menu expanded, which collapses to icons when the canvas plays it;
  desktop with the menu collapsed and the user menu open; narrow (390 px) with the menu closed and open; states:
  loading, error of `GET /me`, no permission, not found; for the theme: the desktop's "Giao diện" menu works in Play
  (its dark theme drawn with those `.dark` values; "Theo hệ thống" drawn as light, since a canvas cannot read the
  system), desktop in the dark theme with the tooltip "Giao diện" open, the error of `GET /me` in the dark theme
  (its destructive text), narrow with the user menu and its submenu "Giao diện" open. No entry: a user whose
  permissions allow no entry sees the menu without its group "Menu", only the store name and the user. SCR-AUT-01
  and SCR-AUT-02, outside the frame, follow the chosen theme too, with no switch of their own.
- **shadcn**: `shadcn add sidebar-07` brings `sidebar`, `collapsible`, `tooltip`, `avatar`, `breadcrumb` and
  `hooks/use-mobile.ts`; it must not overwrite the edited `sheet`, `dropdown-menu`, `button`, `separator`,
  `skeleton` and `input` (DEC-116 (4)). Its files move here (DEC-116 (3)): `app-sidebar` → admin-sidebar-section,
  `team-switcher` → admin-brand, `nav-main` → admin-nav, `nav-user` → user-menu; `nav-projects`, the sample page
  and its data are dropped. `shadcn add empty` brings `empty`; each `add` is committed alone (DEC-116 (1)). The
  theme switch needs no `add`: `dropdown-menu`, `tooltip` and `button` are installed, and `next-themes` too.
- **Texts** (`messages/vi.json`), new ones marked *new*: the menu button `admin.menu.collapse` "Thu gọn menu"
  (*new*) and `admin.menu.expand` "Mở rộng menu" (*new*) on a wide screen, by the menu's state, and
  `admin.menu.open` "Mở menu" on a narrow one, passed as props; the close button of the narrow menu
  `admin.menu.close` "Đóng menu". The English texts of `sidebar.tsx` and `breadcrumb.tsx` go through `ui`
  (DEC-116 (2)): `ui.sidebar.toggle` "Thu gọn hoặc mở rộng menu" (*new*; the rail's label and title, and the
  trigger's default), `ui.sidebar.title` "Menu" (*new*; the narrow sheet's title), `ui.sidebar.description` "Các
  trang quản trị bạn được phép mở." (*new*; the sheet's description, read by screen readers),
  `ui.breadcrumb.label` "Vị trí trang" (*new*; the breadcrumb's name), `ui.breadcrumb.more` "Xem thêm" (*new*;
  the ellipsis). Under the heading of the no-permission page `admin.noPermissionHint` "Vai trò của bạn chưa được
  cấp quyền xem trang này." (*new*), of the 404 `admin.notFoundHint` "Đường dẫn có thể đã sai hoặc trang không
  còn nữa. Vui lòng chọn một mục trong menu hoặc về trang chính." (*new*), and under the button of both
  `admin.needHelp` "Cần trợ giúp? Liên hệ quản trị viên." (*new*; text only: no support channel is specified).
  For the theme: `admin.theme.label` "Giao diện" (*new*; the header button's `aria-label` and Tooltip, the
  menu's label and the user menu's item), `admin.theme.light` "Sáng", `admin.theme.dark` "Tối" and
  `admin.theme.system` "Theo hệ thống" (*new*; the three radio items).
- **Component tree** (DEC-118 (3)), under `web/src/features/shell/admin/components/`; the route
  `app/(admin)/admin/layout.tsx` (a server component that already awaits `connection()`) puts `ThemeProvider` around
  everything it renders, `QueryProvider`, `TooltipProvider`, its children and `AppToaster` included, so that sonner
  follows the chosen theme (DEC-123 (2)): `theme-provider.tsx` of the module `features/shell/theme/` (DEC-126: its
  `index.ts` exports only the provider, since exported by this module it added the whole frame to `/admin/sign-in`,
  98.6 KB of First Load JS), a client component around next-themes
  with attribute class, defaultTheme system, enableSystem, disableTransitionOnChange, storageKey zengear-theme and a
  `nonce` prop, which the layout reads with `headers()` from the request's content-security-policy header
  (`'nonce-…'`, parsed by `nonceOf` of `lib/security-headers.ts`, shared with the tests; DEC-113, no `x-nonce`);
  next-themes sets it only in the server render; `theme-switch.tsx` and the user menu below use only `useTheme`
  (DEC-126 (2)); the provider is not in the tree below, since it wraps
  the layout above the frame; the `<html>` of `app/layout.tsx` gets `suppressHydrationWarning` (DEC-123 (2));
  `app/(admin)/admin/(shell)/layout.tsx` (a server component) reads the cookie `sidebar_state` with `cookies()` for
  the menu's `defaultOpen` (open when there is none) and composes `<AdminShell defaultOpen>` › `<AdminLayout
  defaultOpen sidebar={…} header={…}>` › `<AdminPageSection>{children}`:

  ```text
  admin-shell.tsx                    section: the staff session (useStaffSession, which also guards the routes)
  │                                  and CurrentUserProvider, whose value adds `forbidden` and `clearForbidden`;
  │                                  loading: admin-shell-skeleton, its menu collapsed when defaultOpen is false;
  │                                  error: session-error; ready: its children; the toast region
  │                                  "Thông báo" stays in app/(admin)/admin/layout.tsx, inside ThemeProvider
  ├── admin-layout.tsx               slots: sidebar | header | page — SidebarProvider with defaultOpen (Ctrl/⌘+B
  │                                  toggles; it writes the cookie; it wraps no TooltipProvider of its own, so that
  │                                  the one provider of SCR-AUT-01's Layout sets the delays of **Help on
  │                                  screen**) and SidebarInset rendered as a div, so that the header stays a
  │                                  banner and main#main holds only the page slot; the skip link "Bỏ qua đến nội
  │                                  dung chính", the first focus stop of the page (before the menu and the
  │                                  header), goes to main#main; page padding 24px (16px narrow); narrow (below
  │                                  md): the sidebar becomes a Sheet from the left
  ├── admin-sidebar-section.tsx      Sidebar collapsible="icon": SidebarHeader, SidebarContent, SidebarFooter,
  │   │                              SidebarRail — sidebar
  │   ├── admin-brand.tsx            presentational — SidebarMenuButton size="lg": a square in sidebar-primary
  │   │                              with lucide StoreIcon, then "ZenGear" (app.name; the store name once gap 6 is
  │   │                              closed), a link to /admin, collapsed with that name in a Tooltip; narrow:
  │   │                              the close button "Đóng menu" (icon only, with its Tooltip) beside it
  │   ├── admin-nav.tsx              `nav` named "Menu" around a SidebarGroup labelled "Menu", the entries of
  │   │                              visibleMenu (utils/admin-menu.ts): "Nhân viên" (UsersIcon); "Cài đặt"
  │   │                              (Settings2Icon) as a Collapsible with SidebarMenuSub "Địa điểm kinh doanh",
  │   │                              open when its entry is the current page; collapsed, an entry shows its label
  │   │                              in a Tooltip and a click on "Cài đặt" expands the menu with the group open;
  │   │                              the current entry isActive with aria-current="page" — collapsible, tooltip
  │   └── user-menu.tsx              SidebarMenuButton size="lg" named by `name`: Avatar (aria-hidden) with the
  │                                  first letters of the first and last words of `name`, then `name` and `email`
  │                                  (`name` alone when `email` is null), ChevronsUpDownIcon; DropdownMenu (side
  │                                  right; bottom on a narrow screen): the same header, "Tài khoản của tôi"
  │                                  (UserIcon), then, when `isMobile` of useSidebar (below 768 px), the item
  │                                  "Giao diện" (SunIcon or MoonIcon by next-themes' `resolvedTheme`) as a
  │                                  DropdownMenuSub holding `theme-options.tsx` with `theme` and `setTheme`, then
  │                                  "Đăng xuất" (LogOutIcon); one Tooltip on the button: `name` when collapsed,
  │                                  `name` and `email` when they are cut short; check `max-lines` (it holds 120
  │                                  lines today) — avatar, dropdown-menu, tooltip
  ├── admin-header-section.tsx       header h-16 (h-12 collapsed): SidebarTrigger (texts above, also its
  │   │                              Tooltip), a vertical Separator, Breadcrumb "Vị trí trang" from the route:
  │   │                              the entry's group as text, then the entry ("Cài đặt › Địa điểm kinh
  │   │                              doanh"); a page under an entry (SCR-STF-02, SCR-STF-03) adds the title that
  │   │                              its own Layout names, the entry then a link; the account page "Tài khoản của
  │   │                              tôi"; the no-permission page keeps the requested path; none on /admin and on
  │   │                              the 404; at the right end, unless `isMobile` of useSidebar (so that exactly
  │   │                              one of the switch and the user-menu item shows), `theme-switch.tsx` —
  │   │                              breadcrumb, separator
  │   └── theme-switch.tsx           holds the theme state (useTheme of next-themes): a Button ghost size icon
  │                                  named `admin.theme.label` (SunIcon or MoonIcon by `resolvedTheme`; the
  │                                  header renders after the session, so it is known), in a Tooltip
  │                                  `admin.theme.label`, opening a DropdownMenu (align end) with the label
  │                                  `admin.theme.label`, a separator and `theme-options.tsx` with `theme` and
  │                                  `setTheme` — button, tooltip, dropdown-menu
  ├── theme-options.tsx              presentational, shared by theme-switch and user-menu — props value,
  │                                  onValueChange and the three labels: a DropdownMenuRadioGroup with
  │                                  DropdownMenuRadioItem "Sáng" (light), "Tối" (dark), "Theo hệ thống"
  │                                  (system) — dropdown-menu
  ├── admin-page-section.tsx         the page slot: the no-permission page while a 403 permission-denied holds,
  │                                  else the route's page, the 404 (the catch-all page) included
  ├── admin-shell-skeleton.tsx       presentational — the sidebar (brand, two entries, user), the header, and a
  │                                  title and a line in Skeleton
  ├── session-error.tsx              presentational — in its own `main` (no frame, since GET /me failed), an
  │                                  Empty with an outline, centred, max-w-md: in EmptyHeader TriangleAlertIcon
  │                                  (aria-hidden) in EmptyMedia variant="icon" and the problem's text in
  │                                  EmptyTitle, destructive, with role="alert"; in EmptyContent a row (a div
  │                                  with flex gap-2) of "Thử lại" (default) and "Đăng xuất" (outline) — empty,
  │                                  button
  ├── no-permission.tsx              admin-page-status with ShieldXIcon, "Bạn không có quyền xem trang này.", its
  │                                  hint and the link to the first entry the user can open (or "Tài khoản của
  │                                  tôi"), whose onClick clears the 403 (onLeave)
  ├── admin-not-found.tsx            admin-page-status with SearchXIcon, "Không tìm thấy trang này.", its hint
  │                                  and "Về trang chính" (/admin); exported by the module's index.ts, so that the
  │                                  route app/(admin)/admin/(shell)/not-found.tsx only renders it
  └── admin-page-status.tsx          presentational, no 'use client' (it renders props only) — props icon (an
                                     element, aria-hidden), title, description, href, label, onClick?; an Empty
                                     with an outline filling the page slot (main#main grows to the free height as
                                     a flex column), its content centred, after shadcn's 404 example of Empty: in
                                     EmptyHeader the icon in EmptyMedia variant="icon", the title as an h1 in
                                     EmptyTitle and the description in EmptyDescription; in EmptyContent the link
                                     drawn as a Button (default, asChild), then "Cần trợ giúp? …" in
                                     EmptyDescription — empty, button
  ```

## SCR-AUT-01 Staff sign-in

Staff sign in with their company email and password (UC-001).

| Field | Rule (web) |
|---|---|
| Email ("Email công ty") | required, email format |
| Password ("Mật khẩu") | required; shown as dots, with a show/hide button |

1. "Đăng nhập" signs in with Firebase (email and password).
2. Then `GET /api/v1/admin/me`:
   - `mustChangePassword: true` → SCR-AUT-02;
   - otherwise the `?next=` path, or SCR-STF-01 when the user may open it, or SCR-ACC-01.

| Answer | Shown |
|---|---|
| Firebase: wrong email or password | "Email hoặc mật khẩu không đúng." (never which one) |
| Firebase: too many attempts | "Bạn đã thử quá nhiều lần. Vui lòng thử lại sau." |
| 403 `/problems/account-disabled` | Its `detail` (then signs out) |
| 403 `/problems/account-not-registered` | Its `detail` (then signs out) |

No "forgot password" link in Phase 1: a staff member whose password is lost asks the Admin (UC-001 has no reset
flow).

Texts of the page that the table does not quote: the heading "Đăng nhập nhân viên"; the show/hide button "Hiện mật
khẩu" (one label, its state is `aria-pressed`); "Vui lòng nhập email công ty.", "Email không hợp lệ.", "Vui lòng nhập
mật khẩu."; a network failure of Firebase or of GET `/me`: "Không kết nối được máy chủ. Vui lòng kiểm tra mạng và thử
lại."; any other failure of Firebase: "Không đăng nhập được. Vui lòng thử lại."; an API that refuses GET `/me` right
after Firebase accepted the password (the user is then signed out): the problem's `detail`, or "Đã có lỗi xảy ra. Vui
lòng thử lại." when it has none; Firebase `auth/user-disabled`: "Tài khoản đã bị khóa. Vui lòng liên hệ quản lý cửa
hàng."

### Layout

- **Design**: <https://claude.ai/artifact/Qbvm6mHA3FPL5sLSDVJ43Q>, drawn 2026-11-04 with the design system of
  2026-11-01; approved by the merge of the pull request that wrote this subsection (DEC-119). The shadcn block
  `login-03` (DEC-118 (5)): the store name above one card on the `muted` ground, without the block's social sign-in,
  sign-up link, "forgot password" link and terms text, which Phase 1 does not have.
- **Artboards**: desktop (1440 px), where the show/hide button and "Đăng nhập" (busy) work in Play; narrow (390 px);
  states: field errors with the tooltip of the show/hide button, a sign-in error ("Email hoặc mật khẩu không đúng."),
  the notice after a password change, loading. The page has no empty and no no-permission state.
- **shadcn**: no `add`: `card`, `field`, `input`, `input-group`, `button`, `alert` and `skeleton` are installed, and
  `tooltip` comes with the `shadcn add sidebar-07` of SCR-ADM-01's Layout, which the same pull request (B) builds
  first.
- **Texts** (`messages/vi.json`), new ones marked *new*, by **Help on screen**: the card's description
  `signIn.description` "Đăng nhập để vào khu quản trị của cửa hàng." (*new*); the hint of the email
  `signIn.emailHint` "Email công ty mà cửa hàng đã cấp cho bạn." (*new*) and its placeholder
  `signIn.emailPlaceholder` "ten@example.com" (*new*; the password, only required, has neither); under the button
  `signIn.forgotPassword` "Quên mật khẩu? Liên hệ quản trị viên." (*new*; text only, since
  Phase 1 has no reset flow); the show/hide button's tooltip is its `aria-label` `signIn.showPassword` "Hiện mật
  khẩu". The delays of **Help on screen** come from one `TooltipProvider` per page, so that the next tooltip opens
  at once: one in `app/(admin)/admin/layout.tsx` (sign-in, password change and the shell) with `delayDuration` 700
  and `skipDelayDuration` 300; `Tooltip` of `components/ui/tooltip.tsx` wraps no provider of its own, and
  `SidebarProvider` of `sidebar.tsx` wraps none either (SCR-ADM-01's Layout).
- **Component tree** (DEC-118 (3)), under `web/src/features/identity/sign-in/`; the route
  `app/(admin)/admin/sign-in/page.tsx` composes `<SignInLayout brand={<SignInBrand />}>` around the form in a
  `Suspense` whose fallback is the skeleton:

  ```text
  components/sign-in-layout.tsx      slots: brand | children (the card) — `main` on the `muted` ground,
  │                                  min-h-svh, a column of max-w-sm centred with gap-6 (p-6, md:p-10)
  ├── components/sign-in-brand.tsx   presentational — the square in `primary` with StoreIcon (aria-hidden) and
  │                                  "ZenGear" (app.name), text, not a link: signed out, /admin comes back here
  ├── components/sign-in-form.tsx    section, at most 150 lines (DEC-118 (3)): the Card only; its behaviour moves
  │                                  to hooks/use-sign-in.ts (the session check, ?next= and ?reason=, Firebase,
  │                                  GET /me, the errors of the section's table, the alert's state) and its Zod
  │                                  schema to schemas.ts, as identity/account does; CardHeader centred:
  │                                  CardTitle with the h1 "Đăng nhập nhân viên",
  │                                  CardDescription; CardContent: FieldGroup with, first, an Alert role="status"
  │                                  (CircleCheckIcon) for the notice after a password change or an Alert
  │                                  variant="destructive" (CircleAlertIcon) for a sign-in error; the email Field
  │                                  with its placeholder, its FieldDescription hint tied by aria-describedby,
  │                                  then FieldError; the password Field with an InputGroup whose InputGroupButton
  │                                  icon-xs (EyeIcon /
  │                                  EyeOffIcon, aria-pressed) sits in a Tooltip; the submit Button, full width,
  │                                  disabled with LoaderCircleIcon while busy; a FieldDescription centred with
  │                                  "Quên mật khẩu? …" — card, field, input, input-group, button, alert, tooltip
  └── components/sign-in-skeleton.tsx  presentational — the fallback in the card slot, as high as the form:
                                     the title, the description, two fields with the email's hint, the button and
                                     the line under it in Skeleton, in the same card (the brand stays, from the
                                     layout) — card, skeleton
  ```

## SCR-AUT-02 Required password change

Shown alone, without the menu, while `mustChangePassword` is true (R-USR-03): the staff member replaces the default
password before anything else.

| Field | Rule (web) |
|---|---|
| New password ("Mật khẩu mới") | required, 12 to 128 characters (counted as Unicode code points); spaces count and are kept |
| Repeat ("Nhập lại mật khẩu mới") | the same as the new password (checked by the web only, never sent) |

Help text under the field: "Tối thiểu 12 ký tự. Không dùng mật khẩu mặc định, mật khẩu phổ biến hay chữ zengear."
The web does not check the blocklist or the default password: the API does (R-USR-11).

1. "Đổi mật khẩu" sends `POST /api/v1/admin/me/change-password` `{ newPassword }`.
2. 204: every session of the account is revoked (DEC-094). The web signs out and shows SCR-AUT-01 with "Đã đổi mật
   khẩu. Vui lòng đăng nhập lại bằng mật khẩu mới." The password is never kept after the request.

| Answer | Shown |
|---|---|
| 400 with an item on `#/newPassword` | Under the field, below the help text that names the policies (the default password, the blocklist or zengear; the web has checked the length) |
| 403 `/problems/recent-login-required` | A dialog "Vui lòng nhập lại mật khẩu hiện tại để tiếp tục": re-authenticates with Firebase, then sends the change again once |
| 409 `/problems/staff-account-required` | Toast |

"Đăng xuất" is the only other action on this screen.

Texts of the page that the table does not quote: "Vui lòng nhập mật khẩu mới.", "Mật khẩu mới dài từ 12 đến 128 ký
tự." and "Mật khẩu nhập lại không khớp." (checked by the web); the dialog of 403 `/problems/recent-login-required`
has the description "Lần đăng nhập gần nhất đã quá 5 phút, nên cần xác nhận lại trước khi đổi mật khẩu.", the field
"Mật khẩu hiện tại" ("Vui lòng nhập mật khẩu hiện tại." when empty), "Tiếp tục" and "Hủy"; Firebase's answers there
are "Mật khẩu hiện tại không đúng." for a wrong password and the texts of SCR-AUT-01 for the others; a second 403
after signing in again shows its `detail`; a network failure "Không kết nối được máy chủ. Vui lòng kiểm tra mạng và
thử lại." The sign-in page shows the message of step 2 as a notice, not as an error, until the next sign-in
attempt.

## SCR-ACC-01 My account

The staff member's own account (UC-007): display name and password; the email is read-only.

Loaded from `GET /api/v1/admin/me` (keep its `ETag`).

| Field | API | Rule (web) |
|---|---|---|
| Display name ("Tên hiển thị") | `name` | required, at most 100 characters after trimming |
| Email | `email` | read-only, with the note "Email là tên đăng nhập. Liên hệ quản trị viên khi cần đổi." (gap 5) |

- "Lưu" sends `PATCH /api/v1/admin/me` `{ name }` with `If-Match`; 200 shows "Đã lưu." and refreshes the name in
  the user menu.
- "Đổi mật khẩu" opens the form of SCR-AUT-02 in a dialog, with the same answers; after 204 the user is signed out the
  same way.

| Answer | Shown |
|---|---|
| 400 with an item on `#/name` | Under the field |
| 412 or 428 | Reload, as in *Conventions* |

The form keeps the `ETag` of the values it shows: a refetch of `GET /me` in the background (the window regains
focus) changes neither the field nor that version; the form loads again after 200, 412 and 428. Texts of the page
that the table does not quote: the title "Tài khoản của tôi"; "Vui lòng nhập tên hiển thị." and "Tên hiển thị tối
đa 100 ký tự." (checked by the web, counted in UTF-16 units as the API counts them); the dialog "Đổi mật khẩu" with
the description "Sau khi đổi, bạn và mọi thiết bị đã đăng nhập phải đăng nhập lại bằng mật khẩu mới." and "Hủy".

## SCR-STF-01 Staff list

All staff accounts with their status and roles, to find the one to open, disable or restore (STF-05, DEC-105).

Loaded from `GET /api/v1/admin/staff`. The filters, sort and page are kept in the URL (`?status=&roleId=&sort=&page=`), so a
reload or a shared link shows the same list.

| Column | API | Shown as |
|---|---|---|
| STT | — | (page − 1) × 20 + the row's index in the page's `items`, 1 first; the search hides rows without renumbering |
| Họ tên | `name` | link to SCR-STF-03 |
| Mã nhân viên | `employeeCode` | "—" when empty |
| Email | `email` | |
| Vai trò | `roles[].name` | one badge per role |
| Trạng thái | `status` | badge "Đang hoạt động" (ACTIVE) or "Đã khóa" (DISABLED) |
| Ngày vào làm | `joinedDate` | `dd/MM/yyyy`, "—" when empty |
| Ngày nghỉ việc | `leftDate` | `dd/MM/yyyy`, empty while employed |
| Thao tác | — | a menu of the row's actions (below) |

- **Filters**: "Trạng thái" — Tất cả (no `status`), Đang hoạt động (`ACTIVE`), Đã khóa (`DISABLED`); "Vai trò" — every
  role (`roleId`), shown once the roles can be listed (gap 2).
- **Search box** "Lọc theo tên, mã hoặc email": filters the rows already loaded, in the browser; nothing is sent to
  the API, since names and emails stay out of the query string (DEC-105). With more than one page, the box says
  "Chỉ lọc trong trang đang xem."
- **Sort**: clicking "Họ tên", "Mã nhân viên" or "Ngày vào làm" sends `sort=name|employeeCode|joinedDate`, a second
  click the same with `-`; default `name` (Vietnamese order).
- **Paging**: 20 rows a page (`pageSize=20`), with the total from `totalCount`.
- **Button** "Thêm nhân viên" (SCR-STF-02), shown with `staff.create`.
- **Actions of a row** (chosen by the owner on the change of this Layout): "Xem chi tiết" (SCR-STF-03); with
  `staff.disable`, on another user's account, "Khóa tài khoản" (ACTIVE), or "Khôi phục tài khoản" and "Khóa lại"
  (DISABLED), each opening the same dialog or confirmation as on SCR-STF-03 with the same request; a field error shows
  in the dialog, every other problem in a toast, and a 404, a 409 `concurrency-conflict`, a 500 or no answer also
  reload the list (`hooks/use-staff-status.ts` invalidates the list then too); the dialog closes as the tree of
  SCR-STF-03 says; after a 200 the list reloads. A later action such as the profile update (gap 7) joins this menu.
- **Empty**: no account at all → "Chưa có tài khoản nhân viên."; nothing matches the filters → "Không có nhân viên
  phù hợp với bộ lọc." with a button to clear the filters.

A 400 means the URL was edited by hand: the list falls back to the default filters.

### Layout

- **Design**: <https://claude.ai/artifact/J3zZ3ffqs7PTWEcu83nFfk>, drawn 2026-11-05 with the design system of
  2026-11-05; approved by the merge of the pull request that wrote this subsection (DEC-119); changed 2026-11-05
  (the columns STT and Thao tác, the soft badges), approved by the merge of the pull request of that change. The
  canvas shows the frame of SCR-ADM-01 for context only; this Layout does not approve it.
- **Artboards**: desktop (1440 px), signed in as the Admin, where "Họ tên" sorts and the menu "Thao tác" of a row
  opens in Play (open on the locked row); narrow (390 px), on the first of three pages, with the search hint and the
  tooltip of "Trang sau" open; states: loading, empty (no account), empty (nothing matches the filters), error. No
  permission is the page of SCR-ADM-01 (`RequirePermission` with `staff.read`). The fixture manager is drawn locked,
  so that both status badges show.
- **shadcn**: `shadcn add table badge select pagination` (the tree names them too); it must not overwrite the
  edited `button` that `pagination` depends on (DEC-116 (4)); `pagination` writes English texts, which go through
  `ui`. The change needs no `add`: `dropdown-menu` and `tooltip` are installed.
- **Theme**: soft badges, as the **Theme** bullet of the Layout of SCR-STF-03 says (chosen by the owner): ACTIVE
  `success` with `--success-soft` and `--success-soft-foreground`, DISABLED `secondary` (grey). The run of STF-02
  builds them for both screens (it adds the tokens, changes the variant and `staff-status-badge.tsx`, and removes
  `--success` of commit d6b6032); the pull request that builds this change, `feat(web): add the row actions of the
  staff list (STF-05)`, adds only the two columns and comes after the web-only pull request of STF-02; it is a `feat`,
  not the `refactor(web)` of the paragraph under the screen list, since it adds behaviour (chosen by the owner).
- **Texts** (`messages/vi.json`), new ones marked *new*, under `staff.list` unless named: the card's description
  `description` "Tài khoản nhân viên của cửa hàng, cùng vai trò và trạng thái. Xem, khóa hoặc khôi phục một tài khoản
  từ cột Thao tác." (changed from "Mở một tài khoản để xem, khóa hoặc khôi phục."); `add` "Thêm nhân viên" (*new*);
  the columns `columns.name` "Họ tên", `columns.employeeCode` "Mã nhân viên", `columns.email` "Email", `columns.roles`
  "Vai trò", `columns.status` "Trạng thái", `columns.joinedDate` "Ngày vào làm", `columns.leftDate` "Ngày nghỉ việc"
  (*new*); `status.all` "Tất cả", `status.active` "Đang hoạt động", `status.disabled` "Đã khóa" (*new*; the status
  Field is labelled by `columns.status`); the search box's label `search` "Lọc theo tên, mã hoặc email" (*new*), its
  placeholder `searchPlaceholder` "an@example.com" (*new*) and its hint `searchHint` "Chỉ lọc trong trang đang xem."
  (*new*, shown with more than one page); `empty.title` "Chưa có tài khoản nhân viên." and `empty.description` "Tài
  khoản do quản trị viên hoặc quản lý cửa hàng tạo sẽ hiện ở đây." (*new*); `noMatch.title` "Không có nhân viên phù
  hợp với bộ lọc.", `noMatch.description` "Thử bỏ bớt điều kiện lọc để xem thêm tài khoản." and `clearFilters` "Xóa bộ
  lọc" (*new*); `total` "{count} nhân viên" and `page` "Trang {page} / {pages}" (*new*); the table box's name
  `tableLabel` "Bảng nhân viên" (*new*); `admin.retry` "Thử lại"; `ui.pagination.label` "Phân trang" (*new*; the nav's
  name), `ui.pagination.previous` "Trang trước" and `ui.pagination.next` "Trang sau" (*new*; each both the visible
  text and the `aria-label` of its link, and its tooltip on a narrow screen, where the text hides),
  `ui.pagination.more` "Các trang khác" (*new*); for the change: the columns `columns.number` "STT" with its sr-only
  `columns.numberFull` "Số thứ tự" and `columns.actions` "Thao tác" (*new*; the second is also the tooltip of each
  row's menu button), the button's `aria-label` `actions.label` "Thao tác cho {name}" and the item `actions.view` "Xem
  chi tiết" (*new*), the items `staff.account.disable`, `staff.account.restore` and `staff.account.disableAgain` and
  the texts of the dialog and the confirmation of SCR-STF-03. No cell is cut short: the cells do not wrap and the
  table box scrolls sideways, so no text of the table needs a tooltip.
- **Component tree** (DEC-118 (3)), under `web/src/features/identity/staff-accounts/components/`; the route
  `app/(admin)/admin/(shell)/staff/page.tsx` composes the layout with the three sections inside
  `RequirePermission` (`staff.read`):

  ```text
  staff-list-layout.tsx              slots: header | filters | list — one Card (a one-card page: the h1 in
  │                                  CardTitle); narrow: the filters stack and the table's box gets a border
  ├── staff-list-header-section.tsx  CardHeader: CardTitle with the h1 "Nhân viên" (admin.menu.staff),
  │                                  CardDescription `description`; CardAction: the link "Thêm nhân viên" (PlusIcon,
  │                                  a Button asChild) to SCR-STF-02, shown with `staff.create`, full width on a
  │                                  narrow screen — card, button
  ├── staff-list-filters-section.tsx the Field `search` (InputGroup with SearchIcon and the placeholder
  │                                  `searchPlaceholder`, FieldDescription `searchHint` with more than one page,
  │                                  tied by aria-describedby) and the Field `status`
  │                                  labelled `columns.status` (Select: Tất cả, Đang hoạt động, Đã khóa); status,
  │                                  sort and page in the URL, a change of status drops `page` (back to 1); the
  │                                  search text stays in the page, never in the URL (DEC-105): the route calls
  │                                  `hooks/use-staff-list.ts` once and gives its state to this section and the
  │                                  table section; the role filter left out (gap 2) — field, input-group, select
  └── staff-list-table-section.tsx   renders what `hooks/use-staff-list.ts` gives (the query GET /api/v1/admin/staff
      │                              with the URL's status, sort and page, pageSize 20, the search over the page's rows,
      │                              and the URL changes, so the section stays under 150 lines): the Table of the nine
      │                              columns, in a box with role="region", tabIndex 0 and aria-label `tableLabel` at
      │                              every width, its cells not wrapped; its rows `staff-list-row.tsx`, to which it
      │                              passes whether the user may act (`staff.disable` in `permissions`) and the user's
      │                              `id` (`useCurrentUser`), worked out once; "Họ tên" a link to SCR-STF-03; footer:
      │                              `total` and the Pagination named `ui.pagination.label` ("Trang trước", `page`,
      │                              "Trang sau"; on page 1 and on the last page the link that leads nowhere has no
      │                              href, aria-disabled and opacity 50%; on a narrow screen the two links show only
      │                              their icon, each in a Tooltip with its text); loading: three rows of Skeleton;
      │                              empty (`totalCount` 0 with the default filters): Empty `empty.*` (UsersIcon) with
      │                              "Thêm nhân viên" when `staff.create`; nothing matches (`totalCount` 0 with a
      │                              status, or the search hides every row of the page): Empty `noMatch.*` (SearchXIcon)
      │                              with "Xóa bộ lọc" (outline), which clears the search and `status`; a page past the
      │                              end (`items` empty, `totalCount` above 0) goes back to page 1; a 400 resets the URL
      │                              to the default filters; any other problem: Empty with the problem's text in
      │                              EmptyTitle, destructive, role="alert" (TriangleAlertIcon), and "Thử lại", disabled
      │                              while the retry is pending; each Empty in place of the rows, inside the card, so
      │                              that the filters stay — table, tooltip, empty, skeleton, pagination
      ├── staff-sort-header.tsx      presentational — the header cell of "Họ tên", "Mã nhân viên" or "Ngày vào
      │                              làm", whose button sorts, with aria-sort and ArrowUpIcon (ascending),
      │                              ArrowDownIcon (descending) or ArrowUpDownIcon (not sorted) — button
      ├── staff-roles-cell.tsx       presentational — one Badge (secondary) per role name — badge
      ├── staff-status-badge.tsx     presentational — Badge: "Đang hoạt động" (`success`, soft green), "Đã khóa"
      │                              (`secondary`, grey), built by the run of STF-02 — badge
      └── staff-list-row.tsx         presentational — the nine cells of one account: STT first (right-aligned,
          │                          tabular-nums, its header "STT" `aria-hidden` with an sr-only `columns.numberFull`),
          │                          then the seven of the API and the actions; its skeleton row has nine bars
          └── staff-row-actions.tsx  holds behaviour (the menu's open state and the dialogs): a Button ghost size
                                     icon (MoreHorizontalIcon) named `actions.label`, in a Tooltip
                                     `columns.actions` (shorter than the name on purpose: the name contains it),
                                     opening a DropdownMenu (align end): "Xem chi tiết" (EyeIcon, a link to
                                     SCR-STF-03); when the row says the user may act and the account is not the
                                     user's own, a separator, then on ACTIVE "Khóa tài khoản" (variant
                                     destructive, LockIcon), on DISABLED "Khôi phục tài khoản" (RotateCcwIcon)
                                     and "Khóa lại" (variant destructive, LockIcon); they open
                                     `staff-disable-dialog.tsx` and `staff-restore-dialog.tsx` of SCR-STF-03
                                     with the row's account as props, whose requests come from
                                     `hooks/use-staff-status.ts`; so this change is built after the web-only pull
                                     request of STF-02 (`feat(web): … (STF-02)`) — dropdown-menu, tooltip, button
  ```

## SCR-STF-02 New staff account

The Admin (or a Store Manager holding `staff.create`) creates a staff account with one or more roles (UC-005).

| Field | API | Rule (web) |
|---|---|---|
| Email ("Email công ty") | `email` | required, email format, at most 255 characters |
| Họ tên | `name` | required, at most 100 characters after trimming |
| Mã nhân viên | `employeeCode` | required, at most 50 characters, no space at the start or end |
| Ngày vào làm | `joinedDate` | required; date picker, today by default |
| SĐT công việc | `workPhone` | optional; 8 to 15 digits, may start with + |
| Vai trò | `roleIds` | at least one, at most 20; multi-select of the roles (gap 2), Admin included for every caller: the web never checks a role name (R-SEC-03), and the API refuses a caller who may not give it (R-TX-11, row below) |

1. "Tạo tài khoản" sends `POST /api/v1/admin/staff`.
2. 201 opens SCR-STF-03 of the new account with "Đã tạo tài khoản. Nhân viên đăng nhập bằng mật khẩu mặc định và
   phải đổi mật khẩu ngay lần đầu." The web never shows or knows the default password.

| Answer | Shown |
|---|---|
| 400 with an item on `#/<field>` | Under the field |
| 409 `/problems/email-already-registered`, `/problems/auth-account-exists` (item on `#/email`) | Under Email |
| 409 `/problems/employee-code-taken` (item on `#/employeeCode`) | Under Mã nhân viên |
| 409 `/problems/role-required`, `/problems/role-not-for-staff`, `/problems/admin-role-not-assignable` (item on `#/roleIds`) | Under Vai trò |

## SCR-STF-03 Staff account

One staff account, with the actions to disable or restore it (UC-005, UC-006).

Loaded from `GET /api/v1/admin/staff/{id}`: name, email, employee code, joined date, left date, work phone, status, roles,
and "Chưa đổi mật khẩu mặc định" when `mustChangePassword` is true. No field is editable in Phase 1 (STF-01 creates,
STF-02 disables and restores); the action "Cập nhật thông tin" is designed ahead and waits for gap 7, the photo for
gap 8.

**Action "Khóa tài khoản"** (with `staff.disable`, on an ACTIVE account; on a DISABLED account the same action is
named "Khóa lại", for the case of a 500 below; not shown on the user's own account — the API refuses it anyway, R-USR-12): a dialog

| Field | API | Rule (web) |
|---|---|---|
| Nhân viên nghỉ việc | — | checkbox; when ticked, the date field below is required |
| Ngày nghỉ việc | `leftDate` | date picker, not after today (Asia/Ho_Chi_Minh), not before the joined date |

with the warning "Nhân viên sẽ bị đăng xuất khỏi mọi thiết bị ngay lập tức." "Khóa" sends
`POST /api/v1/admin/staff/{id}/disable` `{ leftDate }` (null when not ticked); 200 shows the account as returned.

**Action "Khôi phục tài khoản"** (with `staff.disable`, on a DISABLED account): a confirmation, then
`POST /api/v1/admin/staff/{id}/restore`; 200 shows the account as returned.

| Answer | Shown |
|---|---|
| 404 | Its `detail` on the page, with a link to SCR-STF-01 |
| 400 with an item on `#/leftDate` | Under the date field |
| 409 `/problems/left-date-before-joined-date`, `/problems/left-date-in-future` (item on `#/leftDate`) | Under the date field |
| 409 `/problems/own-account-not-lockable`, `/problems/last-active-admin`, `/problems/admin-account-not-lockable`, `/problems/admin-account-not-restorable`, `/problems/account-anonymized` | Toast |
| 409 `/problems/concurrency-conflict` | Toast, and reloads |
| 500 on "Khóa" | The account may be disabled with its sessions still open (R-SEC-04), or not disabled at all: the web's text of *Conventions* in a toast. The account is reloaded; "Khóa" (ACTIVE) or "Khóa lại" (DISABLED) sends the request again, which revokes the sessions again |

### Layout

- **Design**: <https://claude.ai/artifact/QjTnaq2rsB49xBeN3Wkuen>, drawn 2026-11-11 with the design system of
  2026-11-11; approved by the merge of the pull request that wrote this subsection (DEC-119). It changes the layout of
  the page only: one centred column, with a section navigation from `xl`; the 2560 px artboard shows the column
  centred on a 2K screen. The dialog "Khóa tài khoản", the confirmation "Khôi phục tài khoản?", the DISABLED account
  and the sheet "Cập nhật thông tin" keep their design of <https://claude.ai/artifact/DuLX9hmqGWyyP9vmoUw6Dd> (pull
  request #111). The canvas shows the frame of SCR-ADM-01 for context only; this Layout does not approve it.
- **Artboards**: desktop (1440 px) of an ACTIVE account, where the section navigation works in Play; wide (2560 px),
  where the column keeps 1024 px and centres; narrow (390 px) of an account whose default password is unchanged, with
  no section navigation; dark (1440 px, DEC-123 (5): soft badges and `destructive-soft` on the dark values of
  DEC-124), with "Trạng thái tài khoản" the current section; states: loading (the navigation shows, the cards as
  Skeleton), 404 and error (an Empty in the column, no navigation). No permission is the page of SCR-ADM-01
  (`RequirePermission` with `staff.read`). The sample is the fixture manager. At the owner's request the heading
  keeps its small Avatar, the details card shows its action "Cập nhật thông tin" (drawn ahead for gap 7, as in the
  previous Layout) and, beside the information, the photo: an Avatar of 160px with the initials, and "Chọn ảnh"
  under it, drawn ahead and built once gap 8 closes, like the sheet of gap 7; the 390 px artboard draws the photo
  at 96px (the heading Avatar at 56px) above the details and "Chọn ảnh" at full width.
- **shadcn**: no `add`. The section navigation is plain links on the tokens (shadcn has no table of contents;
  `sidebar` is the app's menu and too heavy here). It lives in this module (`components/section-nav.tsx`,
  `hooks/use-active-section.ts`) while only this screen uses it (DEC-118 (1), `docs/FRONTEND.md`, *Modules and
  boundaries*); the pull request that builds the Layout of SCR-ACC-01, which takes the same layout, moves both to
  `components/app/` and `@/hooks`.
- **Theme**: unchanged and built: the soft status badges (`success` on `success-soft`, DISABLED `secondary`,
  ANONYMIZED `outline`) and the Button variant `destructive-soft` of the previous Layout of this screen are in
  `app/globals.css` and `components/ui/` since the pull request of STF-02, with their `.dark` values since AUT-04
  (DEC-123 (4), DEC-124). The current link of the section navigation is `accent` with `foreground` text at weight 500;
  the others `muted-foreground` (4.98:1 light, 6.72:1 dark on `background`), on hover `accent` (`muted-foreground` on
  `accent` 4.56:1 light, 4.83:1 dark).
- **Section navigation** (new behaviour, so the build is a `feat(web): … (STF-02)` pull request on
  `feat/stf-02-web-2`, the next free name since `feat/stf-02-web` has merged, CLAUDE.md *Workflow*; as the paragraph
  under the screen list says): from `xl` (1280px) a `nav` labelled by its visible title "Trên trang này"
  (`admin.sectionNav.label`, `text-xs font-medium`, `muted-foreground`) sits left of the cards in a column of 200px,
  `sticky top-6` (24px, the page padding; the header of SCR-ADM-01 scrolls with the page) with `self-start` on its
  grid cell, and lists one link per card, in page order, with the card's title as its text
  (`staff.account.info.title`, `.status.title`); a link text wraps, never cut short. It is navigation, not content, so
  it sits on the page ground like the menu of SCR-ADM-01, an exception to **Grouping**. Each card is a `section` with
  an `id` (`info`, `status`), `aria-labelledby` its title and `scroll-margin-top` 24px; the titles are `h2` with the
  ids `info-title` and `status-title` and `tabindex="-1"`. A click scrolls to the card (smoothly, at once under
  `prefers-reduced-motion`), moves the focus to its `h2` and replaces the URL hash without a new history entry. While
  the page scrolls, the current link (`aria-current="location"`) is the last card whose top has passed a line 24px
  plus a third of the viewport from the top; when the document scrolls at all (`scrollHeight` above the viewport), the
  last card is current once the sentinel after it (`staff-account-end`, an empty `aria-hidden` div of the layout)
  comes into view; when it does not scroll, the first card stays current. One IntersectionObserver watches the
  sections and the sentinel; no handler on every scroll frame. A hash on load (`#info`, `#status`) is applied once the
  account has loaded, not while the Skeleton shows. Below `xl` the navigation is hidden with CSS (its section stays
  mounted, so a hash still scrolls) and the status card comes first.
- **Width**: the page's content is one column of at most 1024px (`max-w-5xl`), centred in the page area of SCR-ADM-01
  (24px padding, 16px narrow); from `xl` it holds the heading over a grid `200px minmax(0, 1fr)` with a gap of 32px
  (navigation | cards), so a card is about 792px wide at most, whatever the screen. `xl` rather than `lg`, because at
  1024px with the menu expanded the card column would be about 488px.
- **Texts** (`messages/vi.json`), new ones marked *new*, under `staff.account` unless named:
  `admin.sectionNav.label` "Trên trang này" (*new*; the visible title of the section navigation, which names the
  `nav`); for gap 8, not built yet: `info.photoAlt` "Ảnh của {name}" (*new*; the alt of the photo once there is
  one), `info.choosePhoto` "Chọn ảnh" and its hint `info.photoHint` "Ảnh chân dung, rõ mặt." (*new*; the formats and
  size limit join the hint with the Decision of gap 8); the page's title and breadcrumb entry `title` "Tài khoản
  nhân viên" (*new*); the details card
  `info.title` "Thông tin nhân viên" and `info.description` "Thông tin ghi khi tạo tài khoản, dùng để liên hệ và phân
  quyền." (*new*); its labels
  `staff.list.columns.email`, `.employeeCode`, `.roles`, `.joinedDate`, `.leftDate`, with `workPhone` "SĐT công việc"
  (*new*), and `staff.list.noValue` "—" for an empty value; the status card `status.title` "Trạng thái tài khoản" and
  `status.description` "Tài khoản bị khóa không đăng nhập được; khôi phục để nhân viên dùng lại." (*new*), its label
  `staff.list.columns.status`, the badges `staff.list.status.active` and `.disabled`, with `status.anonymized` "Đã ẩn
  danh" (*new*); `password` "Mật khẩu" and `mustChangePassword` "Chưa đổi mật khẩu mặc định" (*new*); the actions
  `disable` "Khóa tài khoản", `disableAgain` "Khóa lại" with its hint `disableAgainHint` "Gửi lại lệnh khóa để đăng
  xuất nhân viên khỏi mọi thiết bị, khi lần khóa trước báo lỗi.", `restore` "Khôi phục tài khoản", `ownAccount` "Bạn
  không thể khóa tài khoản của chính mình." and `anonymized` "Dữ liệu cá nhân của tài khoản này đã được ẩn danh, nên
  không khóa hay khôi phục được nữa." (*new*); the dialog `disableDialog.title` "Khóa tài khoản",
  `disableDialog.description` "Tài khoản bị khóa không đăng nhập được cho đến khi được khôi phục.",
  `disableDialog.warning` "Nhân viên sẽ bị đăng xuất khỏi mọi thiết bị ngay lập tức.", `disableDialog.leaving` "Nhân
  viên nghỉ việc" with its hint `disableDialog.leavingHint` "Đánh dấu khi nhân viên đã nghỉ, để ghi ngày làm việc cuối
  cùng.", the date's hint `disableDialog.leftDateHint` "Ngày làm việc cuối cùng: không sau hôm nay và không trước ngày
  vào làm ({joinedDate})." or, without a joined date, `disableDialog.leftDateHintToday` "Ngày làm việc cuối cùng:
  không sau hôm nay.", `disableDialog.submit` "Khóa" (*new*; the date's label is `staff.list.columns.leftDate`); the
  confirmation `restoreDialog.title` "Khôi phục tài khoản?", `restoreDialog.description` "Nhân viên đăng nhập lại được
  bằng mật khẩu đang có, và ngày nghỉ việc được xóa." and `restoreDialog.submit` "Khôi phục" (*new*); `cancel` "Hủy"
  (*new*); the toasts `disabled` "Đã khóa tài khoản." and `restored` "Đã khôi phục tài khoản." (*new*); `backToList`
  "Về danh sách nhân viên" (*new*); `admin.retry` "Thử lại"; `ui.calendar.previousMonth` "Tháng trước" and
  `ui.calendar.nextMonth` "Tháng sau" (*new*; each both the `aria-label` of its button and its tooltip),
  `ui.calendar.nav` "Chọn tháng" (the nav's name), `ui.calendar.today` "hôm nay" and `ui.calendar.selected` "đã chọn"
  (the parts a day button's name adds) (*new*), and a key of the same namespace for any other label the installed
  version reads; `ui.close` "Đóng" (the tooltip of the close button of the dialog and of the sheet);
  `disableDialog.leftDateFuture` "Ngày vào làm sau hôm nay, nên chưa ghi được ngày nghỉ việc." (*new*); for gap 7, not
  built yet: `update` "Cập nhật thông tin", `updateSheet.title` "Cập nhật thông tin nhân viên",
  `updateSheet.description` "Sửa họ tên, mã, số điện thoại và ngày vào làm. Email và vai trò chưa đổi được ở đây.",
  the labels `staff.list.columns.name`, `.employeeCode`, `workPhone`, `staff.list.columns.joinedDate`, the hints
  `updateSheet.nameHint` "Tối đa 100 ký tự.", `updateSheet.employeeCodeHint` "Tối đa 50 ký tự, không để khoảng trắng ở
  đầu hoặc cuối." and `updateSheet.workPhoneHint` "Không bắt buộc; 8 đến 15 chữ số, có thể bắt đầu bằng +.", the
  placeholder `updateSheet.workPhonePlaceholder` "0901234567", `updateSheet.submit` "Lưu" and the toast `updated` "Đã
  cập nhật thông tin." (*new*). The date pickers and the checkbox have no placeholder.
- **Component tree** (DEC-118 (3)), under `web/src/features/identity/staff-accounts/components/`; the route
  `app/(admin)/admin/(shell)/staff/[id]/page.tsx` (its metadata title `staff.account.title`) composes the layout with
  its sections inside `RequirePermission` (`staff.read`) and the client `StaffAccountProvider`
  (`hooks/staff-account-context.tsx`), which holds the one call of `hooks/use-staff-account.ts`: GET
  /api/v1/admin/staff/{id}, the disable and restore mutations of `hooks/use-staff-status.ts`, which the row actions of
  SCR-STF-01 share (by `id`; on 200, and on a 404, a 409 `concurrency-conflict`, a 500 or no answer, each invalidates
  that account's query and the queries of the staff list, so that SCR-STF-01 shows the new status, `docs/FRONTEND.md`,
  *Data from the API*), and whether the user may act (`staff.disable`, and not their own account: `id` of
  `useCurrentUser` from `identity/access`):

  ```text
  staff-account-layout.tsx               slots: heading | nav | details | status — a page of two cards, its
  │                                      heading above them (Grouping), in one column of at most 1024px, centred
  │                                      (Width); from xl: the heading over a grid of the nav slot (200px, sticky
  │                                      top-6, self-start) and the cards stacked, details then status, then an
  │                                      empty `aria-hidden` sentinel div (id `staff-account-end`); below xl the
  │                                      nav slot is hidden with CSS (`hidden xl:block`, so its section stays
  │                                      mounted and the hash still applies), one column, status first so that its
  │                                      action comes before the details; it only places the slots
  ├── staff-account-nav-section.tsx      the nav slot: reads StaffAccountProvider; renders nothing on a 404 or a
  │                                      failed load; while loading and once loaded it builds the items (id info
  │                                      with `staff.account.info.title`, id status with `.status.title`), calls
  │                                      useActiveSection with them and renders SectionNav with the label
  │                                      `admin.sectionNav.label`, the current id and the select handler; scrolls
  │                                      to the hash once the account has loaded
  │   ├── section-nav.tsx                presentational — props: label, items (id, label), currentId, onSelect;
  │   │                                  `nav` aria-labelledby its visible title (`text-xs font-medium`,
  │   │                                  muted-foreground); one link per item to #id, 32px high, radius-md,
  │   │                                  muted-foreground, accent on hover, text wrapping, focus as the base ring of
  │   │                                  button.tsx (`outline-none focus-visible:ring-[3px]
  │   │                                  focus-visible:ring-ring/50`); the current one accent,
  │   │                                  foreground, weight 500, aria-current="location"; a click calls onSelect —
  │   │                                  no shadcn component (moves to components/app/ with SCR-ACC-01)
  │   └── ../hooks/use-active-section.ts the current id: one IntersectionObserver on the sections and the
  │                                      sentinel `staff-account-end` of the layout (the line 24px plus a third
  │                                      of the viewport from the top; the last id at the sentinel only when the
  │                                      document scrolls, else the first); select: scrolls to the section
  │                                      (smooth unless reduced motion), focuses its h2 (`<id>-title`) and
  │                                      replaces the hash (moves to @/hooks with SCR-ACC-01)
  ├── staff-account-heading-section.tsx  an Avatar of 64px (56px narrow), kept at the owner's request in addition to
  │                                      the photo of the details card, `aria-hidden` always: with the initials
  │                                      (`initials` of `@/utils/initials`, as `staff-photo.tsx`) and, once gap 8
  │                                      gives a photo, with AvatarImage `alt=""` (the photo of the details card
  │                                      carries `info.photoAlt`); beside it the h1 `name` with the status badge and
  │                                      a muted line `email · employeeCode` (the email alone when the code is
  │                                      empty; it wraps, never cut short); loading: Skeleton of the avatar and the
  │                                      heading; 404: Empty, the problem's `detail` in EmptyTitle as the page's h1
  │                                      (UserXIcon), no description, the link "Về danh sách nhân viên"
  │                                      (ArrowLeftIcon, a Button asChild) to SCR-STF-01; any other problem: Empty
  │                                      with the problem's text in EmptyTitle as the page's h1, destructive,
  │                                      role="alert" (TriangleAlertIcon), and "Thử lại", disabled while the retry
  │                                      is pending; 401 and 403 as *Conventions* says — avatar, badge, skeleton,
  │                                      empty, button
  ├── staff-account-details-section.tsx  Card as a `section` id info, aria-labelledby info-title; CardTitle
  │                                      `info.title` as an h2 with id info-title and tabindex -1, scroll-margin-top
  │                                      24px on the section; CardDescription `info.description`;
  │                                      CardAction "Cập nhật thông tin" (outline, PencilIcon; full width under
  │                                      the description on a narrow screen), which opens the sheet: not built
  │                                      until gap 7 closes, so the first build shows no button; CardContent is
  │                                      a `@container` sized by the card, not the viewport (the menu changes the
  │                                      card's width): the photo block first in the DOM, centred, then the
  │                                      description list; from `@xl` (36rem) a grid `minmax(0, 1fr) 160px` with
  │                                      a gap of 32px, the photo block placed in the second column
  │                                      (`col-start-2 row-start-1`), so the focus order stays the DOM order; the
  │                                      `dl` in rows of label 136px | value, 16px apart, from `@sm` (24rem), the
  │                                      label over value below it: Email, Mã nhân viên, SĐT công việc, Vai trò (one
  │                                      Badge secondary each), Ngày vào làm, Ngày nghỉ việc (`dd/MM/yyyy`; "—"
  │                                      when empty); a long email wraps, never cut short; the photo block shows
  │                                      "Chọn ảnh" only with the permission the Decision of gap 8 names —
  │                                      loading: the card with Skeleton of its title, six rows and a
  │                                      round photo; on a 404 or a failed load it renders nothing (the heading
  │                                      section shows the Empty) — card, badge, skeleton, avatar, button
  │   ├── staff-photo.tsx                presentational — props: `name`, `photoUrl?`, `onChoose?`; an Avatar of
  │   │                                  160px (96px below `@xl`): AvatarFallback with the initials (the first
  │   │                                  letter of the first and of the last word of `name` that start with a
  │   │                                  letter; one letter for a one-word name; `initials` of `@/utils/initials`),
  │   │                                  the Avatar `aria-hidden` while it shows them (the name is in the h1);
  │   │                                  AvatarImage with the alt `info.photoAlt` once gap 8 gives a photo; under
  │   │                                  it, when `onChoose` is given, drawn ahead and not built until gap 8 closes
  │   │                                  (a Decision on where the photo is stored, who may change it, its formats
  │   │                                  and size, and its removal on anonymization; the section holds the upload):
  │   │                                  the Button outline size sm "Chọn ảnh" (`info.choosePhoto`, ImageUpIcon;
  │   │                                  full width below `@xl`) calling `onChoose`, with its FieldDescription
  │   │                                  `info.photoHint` tied by aria-describedby; the first build passes neither
  │   │                                  `photoUrl` nor `onChoose` and shows the Avatar alone — avatar, button
  │   └── staff-update-sheet.tsx         gap 7, not built: Sheet (right; bottom on a narrow screen): SheetTitle
  │                                      `updateSheet.title`, SheetDescription `updateSheet.description`; the
  │                                      Fields Họ tên (`nameHint`), Mã nhân viên (`employeeCodeHint`), SĐT công
  │                                      việc (`workPhoneHint`, placeholder `workPhonePlaceholder`, type tel) and
  │                                      Ngày vào làm (a date picker like the dialog's), each hint tied by
  │                                      aria-describedby; Email (gap 5) and Vai trò (gap 2) left out; the corner
  │                                      close button in a Tooltip `ui.close`; footer
  │                                      "Lưu" (default) and "Hủy" (outline); the rules of its endpoint come with
  │                                      the Decision of gap 7 — sheet, field, input, popover, calendar, button
  ├── staff-account-status-section.tsx   Card as a `section` id status, aria-labelledby status-title; CardTitle
  │                                      `status.title` as an h2 with id status-title and tabindex -1 (today's
  │                                      tabIndex stays), scroll-margin-top 24px; CardDescription
  │                                      `status.description`; the row "Trạng thái" with the status badge; the row
  │                                      "Mật khẩu" with Badge outline `mustChangePassword` when
  │                                      `mustChangePassword`; with `staff.disable`, the actions in a row at the end
  │                                      of the card: from xl `justify-end`, each button at its own width, on
  │                                      DISABLED "Khôi phục tài khoản" then "Khóa lại", the hint
  │                                      `disableAgainHint` under the row aligned right; below xl each button full
  │                                      width, stacked; the muted lines `ownAccount` and `anonymized` start-aligned
  │                                      at every width; on ACTIVE "Khóa tài khoản" (destructive-soft,
  │                                      LockIcon), which opens the dialog; on DISABLED "Khôi phục tài khoản"
  │                                      (default, RotateCcwIcon), which opens the confirmation, and "Khóa lại"
  │                                      (destructive-soft, LockIcon) with its hint `disableAgainHint` tied by
  │                                      aria-describedby, which opens the dialog; on the user's own account no
  │                                      action but the muted line `ownAccount`; on ANONYMIZED (Badge outline
  │                                      `status.anonymized`) no action but the muted line `anonymized`; each
  │                                      200 shows the account returned and a toast `disabled` or `restored`;
  │                                      the 409s of the table above in a toast, `concurrency-conflict` also
  │                                      reloads; the 500 of "Khóa" in the toast of *Conventions*, then
  │                                      reloads; loading: the card with Skeleton of its title and button; on a
  │                                      404 or a failed load it renders nothing — card, badge, button, skeleton
  │   ├── staff-disable-dialog.tsx       props: the account (`id`, `name`, `joinedDate`, `leftDate`), `open` and
  │   │                                  `onOpenChange`, so that a row of SCR-STF-01 opens it too; Dialog: DialogTitle
  │   │                                  `disableDialog.title`, DialogDescription `disableDialog.description`, Alert
  │   │                                  destructive (CircleAlertIcon) `disableDialog.warning`; the Field (horizontal)
  │   │                                  Checkbox `disableDialog.leaving` with its FieldDescription `leavingHint`; when
  │   │                                  ticked, the Field `staff.list.columns.leftDate`: a date picker (Popover with a
  │   │                                  Button outline showing `dd/MM/yyyy` and CalendarIcon, holding a Calendar of one
  │   │                                  month, Monday first, `required` so that a second click never empties it), days
  │   │                                  after today in Asia/Ho_Chi_Minh and before the joined date disabled, its
  │   │                                  FieldDescription `leftDateHint` (or `leftDateHintToday`) tied by
  │   │                                  aria-describedby, a 400 or 409 on `#/leftDate` in its FieldError; an account
  │   │                                  that has a left date ("Khóa lại") opens ticked with that date, any other opens
  │   │                                  unticked and ticking it picks today in Asia/Ho_Chi_Minh; a joined date after
  │   │                                  today leaves no day to pick: the checkbox is disabled with the FieldDescription
  │   │                                  `leftDateFuture`; an error no field shows at the top in an Alert; footer "Hủy"
  │   │                                  (outline) and "Khóa" (destructive-soft), disabled with LoaderCircleIcon until
  │   │                                  the answer comes; sends `{ leftDate }`, null when not ticked; closes on 200, on
  │   │                                  a 409 shown in a toast, on a 404, on a 500 and on no answer (those three in
  │   │                                  their toast, then a reload); stays open with a field error — dialog, alert,
  │   │                                  field, checkbox, popover, calendar, button
  │   └── staff-restore-dialog.tsx       props: the account (`id`, `name`), `open` and `onOpenChange`, as the dialog's;
  │                                      AlertDialog: AlertDialogTitle `restoreDialog.title`, AlertDialogDescription
  │                                      `restoreDialog.description`; AlertDialogCancel "Hủy" and "Khôi phục" (a Button
  │                                      default, not AlertDialogAction, which would close at once), disabled with
  │                                      LoaderCircleIcon until the answer comes; closes on 200, and on any problem
  │                                      after its toast (a 404, a 500 or no answer also reloads) — alert-dialog, button
  └── staff-status-badge.tsx             (SCR-STF-01) presentational — ACTIVE `success` (soft green), DISABLED
                                         `secondary` (grey), and ANONYMIZED `outline` with `status.anonymized` —
                                         badge
  ```

## SCR-LOC-01 Business location

The Admin edits the store's address, hotline and opening hours (UC-070, R-LOC-01). Phase 1 has one location: the
screen opens it directly; there is no list, no add and no delete.

Loaded from `GET /api/v1/admin/business-locations` (the one item's `id`), then
`GET /api/v1/admin/business-locations/{id}` (keep its `ETag`).

| Field | API | Rule (web) |
|---|---|---|
| Tên cửa hàng | `name` | required, at most 150 characters after trimming |
| Hotline | `phone` | required, 8 to 11 digits |
| Email | `email` | optional, email format, at most 255 characters |
| Địa chỉ | `addressDetail` | required, at most 255 characters after trimming (house number, street) |
| Tỉnh/Thành phố | `provinceId` | required; searchable select (gap 3) |
| Phường/Xã | `wardId` | required; searchable select of the wards of the chosen province (gap 3); cleared when the province changes |
| Giờ mở cửa hằng tuần | `regularHours[]` | for each day Thứ 2 … Chủ nhật (`day` 1–7): closed, or 1 to 3 periods `open`–`close` (`HH:mm`), each opening before it closes, periods of a day not overlapping; at least one period in the week |
| Lịch ngày lễ | `specialHours[]` | at most 100 rows: a date, then "Đóng cửa" (`closed: true`, no hours) or 1 to 3 non-overlapping periods `open`–`close` that replace that day's weekly hours; a date before today is dropped when saving, so the list says "Ngày đã qua sẽ được xóa khi lưu." |

"Lưu" sends `PUT /api/v1/admin/business-locations/{id}` with every field and `If-Match`; 200 shows "Đã lưu." The note under
the title says "Website, phiếu in và email dùng thông tin này."

| Answer | Shown |
|---|---|
| 400 with an item on `#/<field>` | Under the field or the row of hours (`#/regularHours/1`, `#/specialHours`; a province no longer in use is under Tỉnh/Thành phố, a ward outside the province under Phường/Xã) |
| 412 | Reload, as in *Conventions* |

---

## Known gaps

What the API does not offer yet for the screens above. A screen whose row names a gap is built once the gap is closed.
A closed gap keeps its number, since other lines refer to it.

1. **Permissions of the signed-in user** — closed. `GET` and `PATCH /api/v1/admin/me` return `permissions`: the
   permission keys the user's roles grant, sorted, without scope or role names (AUT-04, R-SEC-03). The web uses them
   only to hide menus and buttons (`docs/FRONTEND.md`, Authentication); a change of roles shows at the next call.
2. **Roles to choose from.** No endpoint lists the roles: SCR-STF-02 cannot offer them and SCR-STF-01 cannot filter by
   role. Comes with AUT-05 (Manage Role & Permission).
3. **Provinces and wards.** No endpoint lists provinces and their wards, and no feature describes one; the official
   list of Quyết định 19/2025/QĐ-TTg is not seeded (`api/seed/README.md`) and not in the repository yet; development
   has only the sample rows of `postman/fixtures/LOC-01.sql`. SCR-LOC-01 cannot offer the selects. The address
   book (ACC-02) and the checkout will need the same list. Needs a Decision.
4. **Contract for the web** (DEC-104 (9)) — closed. CORS lets the origins of `Cors:AllowedOrigins` call every route
   and read `ETag` and `Location` (`docs/API_CONVENTIONS.md`, CORS); the OpenAPI document declares the `ETag`
   response header of every versioned resource beside its `If-Match`, and the `enum` values of `status` and `sort` of
   `GET /api/v1/admin/staff`.
5. **Changing a staff member's email.** No endpoint changes it, while UC-007 (2a) and R-USR-04 say the Admin does it in the
   staff management of STF-01, whose feature row has no such criterion; SCR-ACC-01 only points the staff member to the Admin. Needs a feature or a Decision when the Admin must do it.
6. **Store name for every staff member.** SCR-ADM-01 shows the store name at the top of its menu, but `GET
   /api/v1/admin/business-locations` needs `business_location.update` and no endpoint every signed-in staff member may
   read returns it; the menu shows "ZenGear" (message `app.name`) until then. Needs a feature or a Decision (an
   anonymous `GET /api/v1/shop/business-locations` exists, but admin pages call admin endpoints, `docs/FRONTEND.md`).
7. **Updating a staff account's profile.** No endpoint changes a staff member's name, employee code, work phone or
   joined date, and no feature asks for it (Phase 1 only creates, disables and restores). The Layout of SCR-STF-03
   draws the action and its sheet ahead, at the owner's request; they are built once a feature or a Decision
   settles the endpoint, who may use it and its rules. Needs a Decision.
8. **A staff member's photo.** Neither `users` nor `staff_profiles` holds a photo, and no endpoint uploads one; it
   is personal data (where it is stored, who sees it, its removal when an account is anonymized). SCR-STF-03 shows
   the initials in an Avatar until then. Needs a Decision.
