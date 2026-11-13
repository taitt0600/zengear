# SCR-ADM-01 Admin layout and menu

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
  the web's text of [*Conventions*](conventions.md)) with "Thử lại" and "Đăng xuất". The menu shows "ZenGear" instead of the store
  name until gap 6 is closed.
- **Not found**: an `/admin` URL that matches no screen shows "Không tìm thấy trang này." inside the layout, with the
  link "Về trang chính" (`/admin`, the landing page); signed out, it goes to SCR-AUT-01 like any admin route.
- **Toasts** ("Đã lưu." and the problems that *Errors* of [*Conventions*](conventions.md) sends to a toast) sit in a region named "Thông báo".
- **Theme** (DEC-123): "Giao diện" offers "Sáng", "Tối" and "Theo hệ thống", the system theme by default; the choice
  stays in the browser (`localStorage`), never sent to the API. From md up it sits in the header; below md in the
  user menu.

## Layout

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
