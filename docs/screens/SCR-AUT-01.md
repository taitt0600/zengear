# SCR-AUT-01 Staff sign-in

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

## Layout

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
