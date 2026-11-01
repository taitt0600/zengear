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
- **Dates** `yyyy-MM-dd` from the API are shown `dd/MM/yyyy`; timestamps are shown in Asia/Ho_Chi_Minh.
- **Accessibility**: WCAG 2.2 AA; every form works with the keyboard alone, every field has a visible label, errors
  are announced to screen readers.
- **Design system** (DEC-119 (2)): <https://claude.ai/artifact/DAYPEgxSLaHrHYG99Tmntb>, built 2026-10-31 from
  `web/src/app/globals.css` and `web/src/components/ui/` and updated 2026-11-01 to commit d990f0a; `/design-screen`
  brings the system up to date when those files changed since, and then the update's date and commit.
- **Layout** (DEC-119 (3)): the approved design of a screen, a subsection at the end of its section that
  `/design-screen <SCR-ID>` writes and the merge of its Layout pull request approves; a later change of the design is
  a new Layout pull request. The component tree is the part a diff can review, and the runs build from it:

  ````markdown
  ### Layout

  - **Design**: <link>, drawn <yyyy-mm-dd> with the design system of <yyyy-mm-dd>; approved by the merge of the
    pull request that wrote this subsection (DEC-119).
  - **Artboards**: desktop (1440 px), narrow (390 px); states: loading, empty, error, no permission.
  - **Component tree** (DEC-118 (3)), under `web/src/features/<area>/<module>/components/`:

    ```text
    <screen>-layout.tsx                slots: <a> | <b> | <c>; narrow: <what changes>
    ├── <screen>-<block>-section.tsx   <what it shows> — <shadcn component or block>; loading: <…>; empty: <…>
    └── <screen>-<block>-section.tsx   …
        └── <piece>.tsx                presentational — <shadcn component>
    ```
  ````

## Screen list

| ID | Screen | Route | Part | Feature | Permission | Blocked by |
|---|---|---|---|---|---|---|
| SCR-ADM-01 | Admin layout and menu | `/admin` (layout) | Admin | AUT-04 | signed-in staff | |
| SCR-AUT-01 | Staff sign-in | `/admin/sign-in` | Admin | AUT-01 | — | |
| SCR-AUT-02 | Required password change | `/admin/change-password` | Admin | STF-03 | signed-in staff | |
| SCR-ACC-01 | My account | `/admin/account` | Admin | STF-03 | signed-in staff | |
| SCR-STF-01 | Staff list | `/admin/staff` | Admin | STF-05 | `staff.read` | |
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
inside the layout (SCR-STF-01, SCR-LOC-01), so no menu link leads to a 404; the feature replaces it. The screens
built before DEC-119 — SCR-ADM-01, SCR-AUT-01, SCR-AUT-02 and SCR-ACC-01 — need a Layout only when their layout
changes. A `refactor(web)` pull request that changes the layout of a screen (the pull request (B) of DEC-118) needs
that screen's approved Layout first and builds it; a new Layout of a screen `web/` already has (not a placeholder
page) is built the same way, by a `refactor(web)` pull request of a local session, which a Next row of
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

### Layout

- **Design**: <https://claude.ai/artifact/LMwdwkWoxsS3hFQwxE4cXA>, drawn 2026-11-01 with the design system of
  2026-11-01; approved by the merge of the pull request that wrote this subsection (DEC-119). The shadcn block
  `sidebar-07` (a menu that collapses to icons, DEC-118 (5)); the page area is left empty, since every other screen
  draws its own, except on the no-permission page and the 404, which this design draws.
- **Artboards**: desktop (1440 px) with the menu expanded, which collapses to icons when the canvas plays it;
  desktop with the menu collapsed and the user menu open; narrow (390 px) with the menu closed and open; states:
  loading, error of `GET /me`, no permission, not found. No entry: a user whose permissions allow no entry sees the
  menu without its group "Menu", only the store name and the user.
- **shadcn**: `shadcn add sidebar-07` brings `sidebar`, `collapsible`, `tooltip`, `avatar`, `breadcrumb` and
  `hooks/use-mobile.ts`; it must not overwrite the edited `sheet`, `dropdown-menu`, `button`, `separator`,
  `skeleton` and `input` (DEC-116 (4)). Its files move here (DEC-116 (3)): `app-sidebar` → admin-sidebar-section,
  `team-switcher` → admin-brand, `nav-main` → admin-nav, `nav-user` → user-menu; `nav-projects`, the sample page
  and its data are dropped. `shadcn add empty` brings `empty`; each `add` is committed alone (DEC-116 (1)).
- **Texts** (`messages/vi.json`), new ones marked *new*: the menu button `admin.menu.collapse` "Thu gọn menu"
  (*new*) and `admin.menu.expand` "Mở rộng menu" (*new*) on a wide screen, by the menu's state, and
  `admin.menu.open` "Mở menu" on a narrow one, passed as props; the close button of the narrow menu
  `admin.menu.close` "Đóng menu". The English texts of `sidebar.tsx` and `breadcrumb.tsx` go through `ui`
  (DEC-116 (2)): `ui.sidebar.toggle` "Thu gọn hoặc mở rộng menu" (*new*; the rail's label and title, and the
  trigger's default), `ui.sidebar.title` "Menu" (*new*; the narrow sheet's title, its description dropped),
  `ui.breadcrumb.label` "Vị trí trang" (*new*; the breadcrumb's name), `ui.breadcrumb.more` "Xem thêm" (*new*;
  the ellipsis).
- **Component tree** (DEC-118 (3)), under `web/src/features/shell/admin/components/`; the route
  `app/(admin)/admin/(shell)/layout.tsx` (a server component) reads the cookie `sidebar_state` with `cookies()` for
  the menu's `defaultOpen` (open when there is none) and composes
  `<AdminShell defaultOpen><AdminLayout defaultOpen sidebar={…} header={…}><AdminPageSection>{children}…`:

  ```text
  admin-shell.tsx                    section: the staff session (useStaffSession, which also guards the routes)
  │                                  and CurrentUserProvider, whose value adds `forbidden` and `clearForbidden`;
  │                                  loading: admin-shell-skeleton, its menu collapsed when defaultOpen is false;
  │                                  error: session-error; ready: its children; only the toast region
  │                                  "Thông báo" stays in app/(admin)/admin/layout.tsx
  ├── admin-layout.tsx               slots: sidebar | header | page — SidebarProvider with defaultOpen (Ctrl/⌘+B
  │                                  toggles; it writes the cookie) and SidebarInset rendered as a div, so that
  │                                  the header stays a banner and main#main holds only the page slot; the skip
  │                                  link "Bỏ qua đến nội dung chính" before the header goes to main#main; page
  │                                  padding 24px (16px narrow); narrow (below md): the sidebar becomes a Sheet
  │                                  from the left
  ├── admin-sidebar-section.tsx      Sidebar collapsible="icon": SidebarHeader, SidebarContent, SidebarFooter,
  │   │                              SidebarRail — sidebar
  │   ├── admin-brand.tsx            presentational — SidebarMenuButton size="lg": a square in sidebar-primary
  │   │                              with lucide StoreIcon, then "ZenGear" (app.name; the store name once gap 6 is
  │   │                              closed), a link to /admin; narrow: the close button "Đóng menu" beside it
  │   ├── admin-nav.tsx              `nav` named "Menu" around a SidebarGroup labelled "Menu", the entries of
  │   │                              visibleMenu (utils/admin-menu.ts): "Nhân viên" (UsersIcon); "Cài đặt"
  │   │                              (Settings2Icon) as a Collapsible with SidebarMenuSub "Địa điểm kinh doanh",
  │   │                              open when its entry is the current page; collapsed, an entry shows its label
  │   │                              in a Tooltip and a click on "Cài đặt" expands the menu with the group open;
  │   │                              the current entry isActive with aria-current="page" — collapsible, tooltip
  │   └── user-menu.tsx              SidebarMenuButton size="lg" named by `name`: Avatar (aria-hidden) with the
  │                                  first letters of the first and last words of `name`, then `name` and
  │                                  `email` (`name` alone when `email` is null), ChevronsUpDownIcon; DropdownMenu
  │                                  (side right; bottom on a narrow screen): the same header, "Tài khoản của
  │                                  tôi" (UserIcon), "Đăng xuất" (LogOutIcon) — avatar, dropdown-menu
  ├── admin-header-section.tsx       header h-16 (h-12 collapsed): SidebarTrigger (texts above), a vertical
  │                                  Separator, Breadcrumb "Vị trí trang" from the route: the entry's group as
  │                                  text, then the entry ("Cài đặt › Địa điểm kinh doanh"); a page under an entry
  │                                  (SCR-STF-02, SCR-STF-03) adds the title that its own Layout names, the entry
  │                                  then a link; the account page "Tài khoản của tôi"; the no-permission page
  │                                  keeps the requested path; none on /admin and on the 404 — breadcrumb,
  │                                  separator
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
  ├── no-permission.tsx              admin-page-status with ShieldXIcon, "Bạn không có quyền xem trang này." and
  │                                  the link to the first entry the user can open (or "Tài khoản của tôi"),
  │                                  whose onClick clears the 403 (onLeave)
  ├── admin-not-found.tsx            admin-page-status with SearchXIcon, "Không tìm thấy trang này." and "Về
  │                                  trang chính" (/admin); exported by the module's index.ts, so that the route
  │                                  app/(admin)/admin/(shell)/not-found.tsx only renders it
  └── admin-page-status.tsx          presentational, no 'use client' (it renders props only) — props icon (an
                                     element, aria-hidden), title, href, label, onClick?; an Empty with an outline
                                     filling the page slot (main#main grows to the free height as a flex column),
                                     its content centred: in EmptyHeader the icon in EmptyMedia
                                     variant="icon" and the title as an h1 in EmptyTitle; in EmptyContent the
                                     link drawn as a Button (default, asChild) — empty, button
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
| Họ tên | `name` | link to SCR-STF-03 |
| Mã nhân viên | `employeeCode` | "—" when empty |
| Email | `email` | |
| Vai trò | `roles[].name` | one badge per role |
| Trạng thái | `status` | badge "Đang hoạt động" (ACTIVE) or "Đã khóa" (DISABLED) |
| Ngày vào làm | `joinedDate` | `dd/MM/yyyy`, "—" when empty |
| Ngày nghỉ việc | `leftDate` | `dd/MM/yyyy`, empty while employed |

- **Filters**: "Trạng thái" — Tất cả (no `status`), Đang hoạt động (`ACTIVE`), Đã khóa (`DISABLED`); "Vai trò" — every
  role (`roleId`), shown once the roles can be listed (gap 2).
- **Search box** "Lọc theo tên, mã hoặc email": filters the rows already loaded, in the browser; nothing is sent to
  the API, since names and emails stay out of the query string (DEC-105). With more than one page, the box says
  "Chỉ lọc trong trang đang xem."
- **Sort**: clicking "Họ tên", "Mã nhân viên" or "Ngày vào làm" sends `sort=name|employeeCode|joinedDate`, a second
  click the same with `-`; default `name` (Vietnamese order).
- **Paging**: 20 rows a page (`pageSize=20`), with the total from `totalCount`.
- **Button** "Thêm nhân viên" (SCR-STF-02), shown with `staff.create`.
- **Empty**: no account at all → "Chưa có tài khoản nhân viên."; nothing matches the filters → "Không có nhân viên
  phù hợp với bộ lọc." with a button to clear the filters.

A 400 means the URL was edited by hand: the list falls back to the default filters.

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
STF-02 disables and restores).

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
