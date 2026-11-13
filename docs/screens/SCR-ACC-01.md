# SCR-ACC-01 My account

The staff member's own account (UC-007): display name and password; the email is read-only.

Loaded from `GET /api/v1/admin/me` (keep its `ETag`).

| Field | API | Rule (web) |
|---|---|---|
| Display name ("Tên hiển thị") | `name` | required, at most 100 characters after trimming |
| Email | `email` | read-only row; issued by the company (STF-03, gap 5), so no note (owner, 2026-11-12) |
| Mã nhân viên, Vai trò, SĐT công việc, Ngày vào làm | — | read-only rows, once `GET /me` has them (gap 11) |

- "Lưu" sends `PATCH /api/v1/admin/me` `{ name }` with `If-Match`; 200 shows "Đã lưu." and refreshes the name in
  the user menu.
- The password is changed on the page, in its own form instead of a dialog (the owner's choice on the Layout of
  2026-11-12): the current password (added by the run, so that the 5-minute check of DEC-094 never needs a dialog),
  then the new password typed twice, with the requirements of R-USR-11 as a checklist under it. On "Đổi mật khẩu" the
  web checks the form, signs in again with the current password (Firebase `reauthenticateWithCredential`, the call
  of the dialog of 403 `recent-login-required` today), then sends `POST /api/v1/admin/me/change-password`. A wrong
  current password shows under its field, focused, and nothing is sent; the other Firebase answers show at the top of
  the form. A 403 `recent-login-required` should not follow a fresh sign-in; if it does, its `detail` shows in a toast
  and nothing is retried — this screen's exception to **Errors** of [*Conventions*](conventions.md), which asks for the password again.
  After 204 the user is signed out the same way. SCR-AUT-02 keeps its form and its dialog unchanged.

| Answer | Shown |
|---|---|
| 400 with an item on `#/name` | Under the field |
| 412 or 428 | Reload, as in [*Conventions*](conventions.md) |

The form keeps the `ETag` of the values it shows: a refetch of `GET /me` in the background (the window regains
focus) changes neither the field nor that version; the form loads again after 200, 412 and 428. Texts of the page
that the table does not quote: the title "Tài khoản của tôi"; "Vui lòng nhập tên hiển thị." and "Tên hiển thị tối
đa 100 ký tự." (checked by the web, counted in UTF-16 units as the API counts them); the password card's texts are in
its **Layout** below.

## Layout

- **Design**: <https://claude.ai/artifact/MXKwRZTrr5E4XyghiExMU6>, drawn 2026-11-12 with the design system of
  2026-11-11 (commit 39485c9, the current theme and components); approved by the merge of the pull request that wrote
  this subsection (DEC-119). The screen was built before DEC-119 (pull request #87); this Layout follows the
  conventions it predates (**Grouping**, **Help on screen**) and, at the owner's request, takes the layout of
  SCR-STF-03 (a centred column with the section navigation), shows the email and the work details as read-only rows,
  draws the staff member's own photo ahead (gap 8) and changes the password on the page instead of in a dialog. The
  canvas shows the frame of SCR-ADM-01 for context only; this Layout does not approve it.
- **Artboards**: desktop (1440 px), signed in as the fixture manager, where the section navigation works in Play and
  the repeat of the new password does not match yet; wide (2560 px), the column kept at 1024px and centred; narrow
  (390 px), with the photo above the details, the current password refused and every requirement met; state:
  loading. The frame of SCR-ADM-01 shows its own skeleton until `GET /me` answers and `meQuery` stays cached, so the
  page's loading state shows only if that query is ever refetched without data; it is drawn so that the sections
  have one. No error, not found or no permission state: `GET /me` is the query the frame loads and guards, and every
  signed-in staff member may open the page. Light theme only: no colour outside neutral but the field error of
  `destructive` that every form shares (DEC-123 (5)). No icon-only button and no label cut short, so no tooltip.
- **shadcn**: no `add`: `card`, `avatar` and `badge` are installed.
- **Drawn ahead**: the photo and "Chọn ảnh" (gap 8, which now covers a staff member's own photo and amends R-USR-04
  and the STF-03 row) — the first build shows the initials alone; the rows Mã nhân viên, Vai trò, SĐT công việc and
  Ngày vào làm (gap 11) — the first build shows the Email row alone. The owner asked for both (2026-11-12): an
  exception to `/design-screen` step 3, which leaves out what a gap withholds, as on SCR-STF-03.
- **Width and section navigation**: as the bullets **Width** and **Section navigation** of the Layout of SCR-STF-03
  say, with the cards `profile` and `password` (`section`s with those ids, `aria-labelledby` their h2,
  `scroll-margin-top` 24px), the h2 ids `profile-title` and `password-title` with `tabindex="-1"`, and the sentinel
  `account-end`: a column of at most 1024px (`max-w-5xl`), centred; from `xl` the heading over a grid `200px
  minmax(0, 1fr)` with a gap of 32px; below `xl` the navigation is hidden with CSS and the cards stack, profile
  first. The column scrolls in a window of 900px but not on a tall screen, so the moved hook keeps a clicked id
  current until the next scroll moves the line, on both screens (the Layout of SCR-STF-03 says so too). The
  navigation is new behaviour, so the build is a `feat(web): … (STF-03)` pull request on `feat/stf-03-web-2`
  (`feat/stf-03-web` has merged, #87), as the paragraph under the [screen list](README.md#screen-list) says. It comes after the pull request
  of the Next row `layout-scr-stf-03`, which builds `section-nav.tsx`, `use-active-section.ts` and `staff-photo.tsx`
  in the module of SCR-STF-03: this build moves them to `components/app/section-nav.tsx`,
  `@/hooks/use-active-section.ts` (the sentinel id an argument instead of `staff-account-end`) and
  `components/app/profile-photo.tsx` (its texts then props; DEC-118 (1), the second screen that uses them), and
  points SCR-STF-03 at the moved files.
- **Profile card**: as the details card of SCR-STF-03, a `@container` whose photo block comes first in the DOM; from
  `@xl` a grid `minmax(0, 1fr) 200px` with a gap of 32px, the information on the left and the photo block on the
  right (`@xl:border-l @xl:pl-8 @xl:pt-1`); below `@xl` the photo (96px) above the information, centred, and "Chọn
  ảnh" full width (the rule of SCR-STF-03). The information: the name field with "Lưu" beside it on the same line
  (the input grows, the button keeps its width), its hint under both; then the details as divided `dl` rows, a label
  column of 136px (112px narrow) and the value wrapping: Email, then, with gap 11, Mã nhân viên ("—" when empty),
  Vai trò (one `secondary` Badge per role), SĐT công việc ("—" when empty) and Ngày vào làm (`dd/MM/yyyy`, "—" when
  empty).
- **Password card**: the form on the page, in one column of at most 448px (`max-w-md`), full width of the card on a
  narrow screen, with "Đổi mật khẩu" full width below `sm` (`w-full sm:w-auto`; `SubmitButton` takes a
  `className`). Under the new password a checklist of R-USR-11, which the web checks as the user types (as the API
  counts: Unicode code points, `zengear` in any case): "Từ 12 đến 128 ký tự", "Không chứa chữ zengear", and a line
  it cannot check, "Không phải mật khẩu mặc định hay mật khẩu phổ biến (kiểm khi đổi)", which the API checks on the
  send; under the repeat "Giống mật khẩu mới". A met item shows CheckIcon in `foreground`, an unmet one CircleIcon in
  `muted-foreground`, so the two differ in shape and lightness, not hue, and carries a visually hidden "(đạt)" or
  "(chưa đạt)"; the server-checked line is neutral: InfoIcon in `muted-foreground`, no hidden state text, and it
  never blocks. Each field's `aria-describedby` names the ids of its `li` items (the list itself has no
  `aria-label`, which would replace them as the description); the new password's list is named by a visually hidden
  heading `account.passwordRules.label` for a screen reader browsing the page, the repeat's one-item list has no
  name. Nothing is announced while typing. Only length and match block the send, as today (`schemas.ts`, the
  FieldErrors `changePassword.validation.length` and `.mismatch`); the zengear item only shows its state, and a
  password that contains it is refused by the API, whose `detail` shows under the field (DEC-117 (4)), as on
  SCR-AUT-02. R-USR-11 asks for no capital letter, digit or
  symbol (NIST SP 800-63B), so the checklist has none; adding them would be a Decision that changes R-USR-11 and the
  API.
- **Texts** (`messages/vi.json`), new ones marked *new*: the heading `admin.user.account` "Tài khoản của tôi" (the
  page's h1, as today) and its muted line `{name} · {email}` from `GET /me` (no key; the name alone when `email` is
  null); `admin.sectionNav.label` "Trên trang này" (added by the build of SCR-STF-03); the profile card
  `account.profile.title` "Thông tin tài khoản" (*new*) and its description `account.profile.description` "Thông tin
  tài khoản của bạn. Bạn tự sửa được tên hiển thị." (*new*); `account.name` and `account.save` (unchanged); the hint
  of the name `account.nameHint` "Tối đa 100 ký tự." (*new*; the length the web checks); the rows `account.email`
  (unchanged) and, with gap 11, `account.details.employeeCode` "Mã nhân viên", `account.details.roles` "Vai trò",
  `account.details.workPhone` "SĐT công việc", `account.details.joinedDate` "Ngày vào làm" (*new*); for gap 8, not
  built yet, `account.photoAlt` "Ảnh của bạn", `account.choosePhoto` "Chọn ảnh" and its hint `account.photoHint` "Ảnh
  chân dung, rõ mặt. JPG hoặc PNG, tối đa 2 MB." (*new*; the text and limits of SCR-STF-03); the email's note
  `account.emailNote` goes (the owner's choice: the company issues the email). The password card
  `account.passwordCard.title` "Mật khẩu" (*new*) and its description `account.changePasswordNote` "Sau khi đổi, bạn
  và mọi thiết bị đã đăng nhập phải đăng nhập lại bằng mật khẩu mới." (unchanged); the current password
  `changePassword.reauth.password` "Mật khẩu hiện tại", `changePassword.reauth.required` "Vui lòng nhập mật khẩu hiện
  tại." and `changePassword.reauth.wrongPassword` "Mật khẩu hiện tại không đúng." (unchanged), and the other Firebase
  answers `signIn.errors.*` (unchanged, the texts of SCR-AUT-01); the new password `changePassword.newPassword` "Mật
  khẩu mới", the repeat `changePassword.confirmation` "Nhập lại mật khẩu mới" and `changePassword.validation.*`
  (unchanged); the checklist `account.passwordRules.label` "Yêu cầu của mật khẩu mới" (its visually hidden heading),
  `account.passwordRules.length` "Từ 12 đến 128 ký tự", `account.passwordRules.noZengear` "Không chứa chữ zengear",
  `account.passwordRules.serverChecked` "Không phải mật khẩu mặc định hay mật khẩu phổ biến (kiểm khi đổi)",
  `account.passwordRules.match` "Giống mật khẩu mới", `account.passwordRules.met` "(đạt)" and
  `account.passwordRules.unmet` "(chưa đạt)" (*new*; SCR-AUT-02 keeps its hint `changePassword.help`); the button
  `account.changePassword` "Đổi mật khẩu" (unchanged). No placeholder: the name has no format and the others are
  password fields.
- **Component tree** (DEC-118 (3)), under `web/src/features/identity/account/components/`, from `GET /me` (`meQuery`
  of `identity/access`, the query the admin layout holds); the route `app/(admin)/admin/(shell)/account/page.tsx`
  passes the four slots below instead of `title`, `profile` and `password`, and `index.ts` exports the new sections:

  ```text
  account-layout.tsx                     slots: heading | nav | profile | password — a page of two cards, its heading
  │                                      above them (Grouping), in one column of at most 1024px, centred; from xl:
  │                                      the heading over a grid of the nav slot (200px, sticky top-6, self-start)
  │                                      and the cards stacked, profile then password, then an empty `aria-hidden`
  │                                      sentinel div (id `account-end`); below xl the nav slot is hidden with CSS
  │                                      (`hidden xl:block`, so the hash still applies) and the cards stack; it only
  │                                      places the slots (replaces the `max-w-lg` column and its Separator)
  ├── account-heading-section.tsx        an Avatar of 64px (56px narrow), `aria-hidden` always, with the initials of
  │                                      `name` (`initials` of `@/utils/initials`) and, once gap 8 gives a photo,
  │                                      AvatarImage `alt=""`; beside it the h1 `admin.user.account` and a muted
  │                                      line `name · email` that wraps, never cut short; loading: Skeleton of the
  │                                      avatar and of the line, the h1 shown
  ├── account-nav-section.tsx            the nav slot: builds the items (id profile with `account.profile.title`, id
  │                                      password with `account.passwordCard.title`), calls useActiveSection (from
  │                                      `@/hooks`, with the sentinel `account-end`) and renders SectionNav (from
  │                                      `@/components/app`) with the label `admin.sectionNav.label`; shown while
  │                                      loading too; scrolls to the hash once GET /me has answered
  ├── account-profile-section.tsx        `section` id profile, aria-labelledby `profile-title`, scroll-margin-top
  │   │                                  24px — Card: CardTitle as the h2 `account.profile.title` (tabindex -1),
  │   │                                  CardDescription `account.profile.description`; CardContent a `@container`
  │   │                                  with the photo block, the form and the details (Profile card); loading:
  │   │                                  the card with its title and description, the rest as Skeleton
  │   ├── profile-photo.tsx              presentational, moved from SCR-STF-03 to `components/app/` — props `name`,
  │   │                                  `photoUrl?`, `onChoose?` and its texts (alt, button, hint): an Avatar of
  │   │                                  160px (96px below `@xl`) with the initials, `aria-hidden` while it shows
  │   │                                  them; AvatarImage with `account.photoAlt` once gap 8 gives a photo; with
  │   │                                  `onChoose`, the Button "Chọn ảnh" (outline, sm, ImageUpIcon, full width
  │   │                                  below `@xl`) and its hint `account.photoHint` tied by aria-describedby;
  │   │                                  the first build passes neither and shows the Avatar alone
  │   ├── account-profile-fields.tsx     presentational — the name form as today (Field, Input, FormProblems,
  │   │                                  SubmitButton with a `className`): label, then the Input and "Lưu" on one
  │   │                                  line (`flex gap-2`, the Input `flex-1 min-w-0`), then the FieldDescription
  │   │                                  `account.nameHint` tied by aria-describedby (the FieldError after it when
  │   │                                  invalid); the email Input and its note go
  │   └── account-details.tsx            presentational — the `dl` of read-only rows (Profile card) from `GET /me`:
  │                                      Email; with gap 11 the four rows, Vai trò as `secondary` Badges
  └── account-password-section.tsx       `section` id password, aria-labelledby `password-title`, scroll-margin-top
      │                                  24px — Card: CardTitle as the h2 `account.passwordCard.title` (tabindex
      │                                  -1), CardDescription `account.changePasswordNote`; CardContent with the
      │                                  form; replaces the Dialog
      ├── account-password-form.tsx      the form on the page, at most 448px: a hidden `autocomplete="username"`
      │                                  field with the email (as `change-password-form.tsx`); "Mật khẩu hiện tại"
      │                                  (`current-password`) with its FieldError; "Mật khẩu mới"
      │                                  (`new-password`) with the PasswordRules of R-USR-11 tied by
      │                                  aria-describedby; "Nhập lại mật khẩu mới" (`new-password`) with the
      │                                  PasswordRules item "match"; FormProblems at the top; SubmitButton
      │                                  `account.changePassword` (`w-full sm:w-auto`). It uses
      │                                  `useChangePassword` with a new optional step run before the send (no
      │                                  third copy of its mutation and its 400 handling, CODING_GUIDE section 3):
      │                                  the step signs in again with the current password (the call of
      │                                  `reauth-dialog.tsx`); a wrong password becomes the field's FieldError
      │                                  `changePassword.reauth.wrongPassword`, focused, and the other Firebase
      │                                  answers (`signIn.errors.*`) go to FormProblems, unlike the dialog, which
      │                                  shows all of them in an Alert; nothing is sent then. On this page a 403
      │                                  `recent-login-required` opens no dialog: its `detail` in a toast, no retry
      │                                  (Password section above). The passwords are read from the form, never
      │                                  kept as mutation variables, and the form resets after 204, then the
      │                                  sign-out of DEC-094; the button stays disabled until the page changes
      └── password-rules.tsx             presentational — props: the items (id, text, state met, unmet or
                                         neutral) and an optional visually hidden heading; a `ul` without
                                         `aria-label`, each `li` with its id, CheckIcon, CircleIcon or InfoIcon
                                         (16px, aria-hidden), the text and, unless neutral, a visually hidden
                                         `met`/`unmet` text; the field's aria-describedby lists the item ids
                                         (Password card); no aria-live
  ```

  After the build, `change-password-form.tsx` serves SCR-AUT-02 alone: its `onCancel` prop and the mention of
  SCR-ACC-01 in its comment go. Left out: changing the email (gap 5).
