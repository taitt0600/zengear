# SCR-AUT-01 Staff sign-in

Staff sign in with their username or company email and their password (UC-001, DEC-145 (4)).

| Field | Rule (web) |
|---|---|
| Username or email ("Tên đăng nhập hoặc email") | required; trimmed; a value with `@` must have the email format |
| Password ("Mật khẩu") | required; shown as dots, with a show/hide button |

1. "Đăng nhập" signs in with Firebase by email and password. A value with `@` is the email as typed; a value
   without one is a username, and the web signs in with the email R-USR-24 gives it: the username in lower case, `@`
   and the company domain of the configuration (`Staff:EmailDomain`, the same in the API and the web, DEC-145). No
   endpoint looks a username up, so the page tells nobody whether an account exists (gap 15 until the pull request
   of R-USR-24 merges: no account has a username yet).
2. Then `GET /api/v1/admin/me`:
   - `mustChangePassword: true` → SCR-AUT-02;
   - otherwise the `?next=` path, or SCR-STF-01 when the user may open it, or SCR-ACC-01.

| Answer | Shown |
|---|---|
| Firebase: wrong username, email or password; with a username, also `auth/invalid-email` (a username that makes no valid address) | "Tên đăng nhập, email hoặc mật khẩu không đúng." (never which one) |
| Firebase: too many attempts | "Bạn đã thử quá nhiều lần. Vui lòng thử lại sau." |
| 403 `/problems/account-disabled` | Its `detail` (then signs out) |
| 403 `/problems/account-not-registered` | Its `detail` (then signs out) |

No "forgot password" link in Phase 1: a staff member whose password is lost asks the Admin (UC-001 has no reset
flow).

Texts of the page that the table does not quote: the heading "Đăng nhập nhân viên"; the show/hide button "Hiện mật
khẩu" (one label, its state is `aria-pressed`); "Vui lòng nhập tên đăng nhập hoặc email.", "Email không hợp lệ.",
"Vui lòng nhập mật khẩu."; a network failure of Firebase or of GET `/me`: "Không kết nối được máy chủ. Vui lòng
kiểm tra mạng và thử lại."; any other failure of Firebase: "Không đăng nhập được. Vui lòng thử lại."; an API that
refuses GET `/me` right after Firebase accepted the password (the user is then signed out): the problem's `detail`,
or "Đã có lỗi xảy ra. Vui lòng thử lại." when it has none; Firebase `auth/user-disabled`: "Tài khoản đã bị khóa. Vui lòng liên hệ quản lý cửa
hàng."

## Layout

- **Design**: <https://claude.ai/artifact/Qbvm6mHA3FPL5sLSDVJ43Q>, drawn 2026-11-04 with the design system of
  2026-11-01; approved by the merge of the pull request that wrote this subsection (DEC-119). The shadcn block
  `login-03` (DEC-118 (5)): the store name above one card on the `muted` ground, without the block's social sign-in,
  sign-up link, "forgot password" link and terms text, which Phase 1 does not have.
- **Change of 2026-11-14, #158** (DEC-141 (4)): canvas version `1794688615-9470` (the version approved before it, by the
  pull request of 2026-11-04: `1793758178-edbd`), drawn with the design system of 2026-11-13 (commit 563a162); the six
  artboards are named `[Đổi 14/11/2026 · #158]`; approved by the merge of the pull request that wrote this bullet.
  **Required fields**: `admin.requiredNote` (*new* unless an earlier build added it, with `admin.requiredMark`; the texts of SCR-STF-02) under the CardDescription (centred like it; a line of Skeleton more on
  the loading artboard), "Email công ty" and "Mật khẩu" end with ` *` and their inputs are `required`; the tree adds
  both to `sign-in-form.tsx` and the line to `sign-in-skeleton.tsx`. The note's key lives under `admin` although this
  page is outside the frame, so that every form shares one text. **Help on screen**, **Grouping** and **Design
  principles**: met as drawn (the review is in the pull request), nothing else changes.
- **Change of 2026-11-21, #319** (DEC-145 (4), R-USR-24, gap 15; issue #315): canvas version `1795233140-c634` (the
  version approved before it: `1794688615-9470`); the artboards Main, Narrow, Validation, Error and Notice are named
  `[Đổi 21/11/2026 · #319]`, Loading is unchanged. The field "Tên đăng nhập hoặc email" replaces "Email công ty"
  (an Input `type="text"`, `autoComplete="username"`, `autoCapitalize="none"`, `spellCheck={false}`, so that a
  password manager keeps what was saved under the email and a phone does not capitalise a username), with its hint and
  a username as its placeholder; the sign-in error names all three, never which was wrong. The samples come from the
  cast of DEC-146: the placeholder AnNVZG260004 (Nguyễn Văn An), the Error artboard signing in as the Store Manager
  (HaLTZG260002), the Validation artboard with `haltzg260002@`. Approved by the merge of the pull request that wrote
  this bullet.
- **Builds**:
  - The page `web/` has keeps the field "Email công ty" under the Layout of #158 until gap 15 closes with the pull
    request of R-USR-24 (issue #246), since a username signs in only once accounts have one; the row of
    `docs/screens/README.md` holds the screen on gap 15.
  - The change of #319 is built after that merge by a `feat(web): … (AUT-01)` pull request on `feat/aut-01-web`
    (issue #320 `AUT-01 web`, blocked by #246; DEC-148 (6), DEC-123), with the domain setting `Staff:EmailDomain` of
    the web; which run makes that pull request for a screen `web/` already has is a Needs decision item of #319.
  - The open Task #250 (`refactor(web)`) builds only the required marks of #158 on "Email công ty" and "Mật khẩu";
    gap 15 does not hold it, and it builds nothing of #319.
- **Artboards**: desktop (1440 px), where the show/hide button and "Đăng nhập" (busy) work in Play; narrow (390 px);
  states: field errors (an email without its domain) with the tooltip of the show/hide button, a sign-in error ("Tên
  đăng nhập, email hoặc mật khẩu không đúng."), the notice after a password change, loading. The page has no empty and
  no no-permission state.
- **shadcn**: no `add`: `card`, `field`, `input`, `input-group`, `button`, `alert` and `skeleton` are installed, and
  `tooltip` comes with the `shadcn add sidebar-07` of SCR-ADM-01's Layout, which the same pull request (B) builds
  first.
- **Texts** (`messages/vi.json`), new ones marked *new*, by **Help on screen**: the card's description
  `signIn.description` "Đăng nhập để vào khu quản trị của cửa hàng."; since #319 the field's label
  `signIn.identifier` "Tên đăng nhập hoặc email" (*new*, in place of `signIn.email`), its hint
  `signIn.identifierHint` "Tên đăng nhập hoặc email công ty mà cửa hàng đã cấp cho bạn." (*new*, in place of
  `signIn.emailHint`) and its placeholder `signIn.identifierPlaceholder` "AnNVZG260004" (*new*, in place of
  `signIn.emailPlaceholder`: a username of the pattern of R-USR-24, Nguyễn Văn An of the cast of DEC-146, which the
  hint does not repeat), the check `signIn.validation.identifierRequired` "Vui lòng nhập tên đăng nhập hoặc email."
  (*new*, in place of `emailRequired`; `emailInvalid` stays, for a value with `@`) and `signIn.errors.wrongCredentials` "Tên đăng nhập,
  email hoặc mật khẩu không đúng." (changed); the password, only required, has neither hint nor placeholder; under
  the button `signIn.forgotPassword` "Quên mật khẩu? Liên hệ quản trị viên." (*new*; text only, since
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
  │                                  variant="destructive" (CircleAlertIcon) for a sign-in error; the Field
  │                                  "Tên đăng nhập hoặc email" (since #319; use-sign-in.ts turns a username into
  │                                  its email) with its placeholder, its FieldDescription hint tied by
  │                                  aria-describedby, then FieldError; the password Field with an InputGroup
  │                                  whose InputGroupButton icon-xs (EyeIcon / EyeOffIcon, aria-pressed) sits
  │                                  in a Tooltip; the submit Button, full width,
  │                                  disabled with LoaderCircleIcon while busy; a FieldDescription centred with
  │                                  "Quên mật khẩu? …" — card, field, input, input-group, button, alert, tooltip
  └── components/sign-in-skeleton.tsx  presentational — the fallback in the card slot, as high as the form:
                                     the title, the description, two fields with the first one's hint, the button and
                                     the line under it in Skeleton, in the same card (the brand stays, from the
                                     layout) — card, skeleton
  ```
