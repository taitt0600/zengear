# SCR-STF-02 New staff account

A holder of `staff.create` (the Admin, and by default the Store Manager and the IT Support, DEC-128 (1)) creates a staff account with one or more roles (UC-005).

| Field | API | Rule (web) |
|---|---|---|
| Email ("Email công ty") | `email` | required, email format, at most 255 characters |
| Họ tên | `name` | required, at most 100 characters after trimming |
| Mã nhân viên | `employeeCode` | required, at most 50 characters, no space at the start or end |
| Ngày vào làm | `joinedDate` | required; date picker, today by default |
| SĐT công việc | `workPhone` | optional; 8 to 15 digits, may start with + |
| Vai trò | `roleIds` | at least one, at most 20; multi-select of the roles of `GET /api/v1/admin/staff/assignable-roles` (gap 2), the caller's manageable set (R-SEC-12): the web never checks a role name (R-SEC-03), and the API refuses a role outside that set (R-TX-11, R-SEC-12, row below) |

1. "Tạo tài khoản" sends `POST /api/v1/admin/staff`.
2. 201 opens SCR-STF-03 of the new account with "Đã tạo tài khoản. Nhân viên đăng nhập bằng mật khẩu mặc định và
   phải đổi mật khẩu ngay lần đầu." The web never shows or knows the default password. With STF-09, a 201 whose
   `status` is `PENDING` shows "Đã tạo tài khoản, đang chờ duyệt. Nhân viên đăng nhập được sau khi một người có quyền
   duyệt tài khoản này." instead: the web shows the status the API answers and never decides it.

**A PENDING account** (STF-09, R-USR-17, DEC-128 (3)): an account created by someone without `staff.approve` waits
for another person's approval and cannot sign in until then. For a user whose `permissions` (`GET /api/v1/admin/me`)
lack `staff.approve`, the form shows a notice before its fields, "Tài khoản bạn tạo sẽ chờ duyệt"; it only informs,
since the API sets the status. The account then appears on the tab Chờ duyệt of SCR-STF-01.

**Not on this screen**: the HR profile (Hồ sơ nhân sự of STF-10: identity document, birth date, personal contact,
contract, documents). The staff member confirms the notice on personal data and enters their contact data on
SCR-ACC-01 (DEC-130 (2), (7)); a user with `staff.pii.update` enters the rest on SCR-STF-03 (DEC-130 (2), (3)). This
form takes only the staff profile and the sign-in account, collecting no personal data the account does not need
(DEC-130 (1)).

| Answer | Shown |
|---|---|
| 400 with an item on `#/<field>` | Under the field |
| 409 `/problems/email-already-registered`, `/problems/auth-account-exists` (item on `#/email`) | Under Email |
| 409 `/problems/employee-code-taken` (item on `#/employeeCode`) | Under Mã nhân viên |
| 409 `/problems/role-required`, `/problems/role-not-for-staff`, `/problems/admin-role-not-assignable`, `/problems/role-out-of-scope` (item on `#/roleIds`) | Under Vai trò |

## Layout

- **Design**: <https://claude.ai/artifact/BBYRvdq6Pg2dRgMncSahww>, drawn 2026-11-14 with the design system of
  2026-11-13 (commit 563a162); approved by the merge of the pull request that wrote this section (DEC-119). The canvas
  shows the frame of SCR-ADM-01 as its Layout has it (the group "Menu", "Cài đặt" with its submenu closed, the user
  menu at the foot, the theme switch in the header), for context only, except its new submenu "Nhân viên" (the artboard "Menu"), which
  this pull request also approves at the owner's request and records in [SCR-ADM-01](SCR-ADM-01.md) (**Menu**, *Layout*). The breadcrumb of this page is "Nhân viên ›
  Thêm nhân viên", the group as text, then the entry `admin.menu.staffNew` (SCR-ADM-01, *Layout*). It draws the PENDING account of STF-09 (above): the notice
  and the toast of a 201 whose `status` is `PENDING`. At the owner's request (2026-11-14) the required fields carry
  ` *`, which **Required fields** of *Conventions* now sets for every form.
- **Builds**, each part by its feature:
  - the web-only run of STF-01 (`feat(web): … (STF-01)` on `feat/stf-01-web`, since the route is a placeholder page):
    the page, the form, its states and the toast of an ACTIVE account.
  - STF-09 (its web part, with its parts of SCR-STF-01 and SCR-STF-03): the notice and the toast of a PENDING account;
    `staff.approve` and the status `PENDING` are not in the API yet (`UserStatus` has `ACTIVE`, `DISABLED`,
    `ANONYMIZED`). Whichever of the two runs comes second adds its part to the page as it is then.
- **Artboards**, desktop 1440 px unless named: the form signed in as the fixture admin (every assignable role, today in
  Ngày vào làm); the form signed in as the fixture IT Support, filled with the sample "Thu ngân mới (fixture)" (not a
  fixture yet; FX0010, thungan.moi@zengear.test, Cashier ticked), with the notice and the date picker open, its
  month and year dropdowns and the tooltip "Tháng sau" open; errors under their fields (the 409 of Email, the 409 of
  Mã nhân viên and the check of Vai trò, from different submits, drawn together); the same in dark (DEC-123 (5): the
  `destructive` texts and borders); loading (the roles as Skeleton); empty (no role the user may give); error (the
  roles failed to load); sending (the button busy); the toasts after a 201, ACTIVE and PENDING, over SCR-STF-03 (not
  drawn again). Narrow (390 px): the IT Support form, the frame's menu as a sheet, the buttons stacked at full width
  with the primary on top. No permission is the page of SCR-ADM-01 (`RequirePermission` with `staff.create`, as the
  placeholder has it). The only icon-only buttons are the calendar's month buttons, in a `Tooltip` as
  `components/ui/calendar.tsx` has them; no label is cut short.
- **shadcn**: no `add` (`card`, `field`, `input`, `checkbox`, `popover`, `calendar`, `button`, `alert`, `skeleton`,
  `empty` and `sonner` are installed).
- **Theme**: unchanged. The notice is the `alert` default variant with ClockIcon, the icon of the badge "Chờ duyệt" of
  SCR-STF-01; "Tạo tài khoản" is the Button `default` (the one primary action), "Hủy" the Button `outline`.
- **One card**: the form is one task, so it is one `Card` with the page's heading in its `CardTitle` (**Grouping**),
  at most 672px wide (`max-w-2xl`) and centred in the page area, one column of fields like the other admin forms
  (SCR-ACC-01, SCR-LOC-01). Inside, two `FieldSet`s split by a `FieldSeparator` group the fields as SCR-STF-03 groups
  the account's cards: "Hồ sơ nhân viên" (Họ tên, Mã nhân viên, Ngày vào làm, SĐT công việc) and "Tài khoản" (Email
  công ty, Vai trò). The notice comes first in the content, before anything is typed. The actions sit in the
  `CardFooter`, right-aligned: "Hủy" (a link to SCR-STF-01) then "Tạo tài khoản".
- **Behaviour**: every field but SĐT công việc is required and marked ` *` (**Required fields**). Ngày vào làm starts
  on today in Asia/Ho_Chi_Minh; its calendar has month and year dropdowns from 50 years back to 1 year ahead (a store
  enters staff who joined long ago, and one who starts soon), Monday first, today marked. Vai trò is a group of
  `Checkbox`es, two columns from `sm`, one below, in the order of `GET /api/v1/admin/staff/assignable-roles` (all
  pages), none ticked. The web checks the shape with the limits of the table above before it sends; the API checks
  again. While the roles load, fail or come back empty, "Tạo tài khoản" is disabled; a failed load keeps what was
  typed and offers "Thử lại", disabled while the retry is pending. While the request runs the button is disabled with
  LoaderCircleIcon and the fields stay as typed. A 201 opens SCR-STF-03 of the new account (`Location`, or the `id` of
  the body) with the toast of its `status`. An error moves the focus to the first field that shows one; items no
  field shows go in `FormProblems` (`components/app/form-problems.tsx`) at the top of the card's content, under the
  notice; `FormProblems` takes the focus when no field shows an error, as on the other admin forms.
- **Wording**: the hints say the limits only (no "Không bắt buộc", which the ` *` of the other fields now carries);
  the sheet "Cập nhật thông tin" of SCR-STF-03, drawn ahead for gap 7, takes the same hints and marks with its next
  Layout.
- **Texts** (`messages/vi.json`), new ones marked *new*, under `staff.create` unless named: the page's title, breadcrumb
  entry and `CardTitle` `staff.list.add` "Thêm nhân viên"; the `CardDescription` `description` "Tạo tài khoản đăng
  nhập cho nhân viên mới và gán vai trò. Nhân viên đăng nhập bằng mật khẩu mặc định và phải đổi mật khẩu ngay lần
  đầu." (*new*), then `admin.requiredNote` "Trường có dấu <mark>*</mark> là bắt buộc." (*new*, **Required fields**; a rich text,
  `t.rich`, whose `mark` renders the `*` `aria-hidden`) with `admin.requiredMark` "sao" (*new*, the visually hidden
  word in place of the `*`); the legends `profile.title` "Hồ sơ
  nhân viên" (*new*, the words of SCR-STF-03, under a key of its own so this page does not wait for the build of
  `layout-scr-stf-03`, which renames `staff.account.info.title`) with the `FieldDescription` `profile.description`
  "Thông tin của nhân viên tại cửa hàng, dùng để liên hệ trong công việc." (*new*) and `account.title` "Tài khoản"
  (*new*) with `account.description` "Email đăng nhập và vai trò quyết định nhân viên mở được những trang nào."
  (*new*); the labels `staff.list.columns.name`, `.employeeCode`, `.joinedDate`, `staff.account.workPhone`, `email`
  "Email công ty" (*new*) and `staff.list.columns.roles`; the hints (`FieldDescription`, all *new*) `nameHint` "Viết đủ
  dấu. Tối đa 100 ký tự.", `employeeCodeHint` "Mã do cửa hàng cấp, không trùng với nhân viên khác. Tối đa 50 ký tự,
  không có khoảng trắng ở đầu hoặc cuối.", `joinedDateHint` "Mặc định là hôm nay; chọn ngày khác nếu nhân viên đã vào
  làm từ trước hoặc sẽ vào làm sau.", `workPhoneHint` "Từ 8 đến 15 chữ số, có thể bắt đầu bằng dấu +.", `emailHint`
  "Nhân viên đăng nhập bằng email này. Tối đa 255 ký tự." and `rolesHint` "Chọn ít nhất một vai trò. Danh sách chỉ có
  các vai trò bạn được phép gán."; the placeholders (*new*) `workPhonePlaceholder` "0901234567" and `emailPlaceholder`
  "an@example.com" (none in Họ tên, Mã nhân viên, which has no fixed pattern, or the date picker); the calendar's
  `ui.calendar.previousMonth` "Tháng trước" and `.nextMonth` "Tháng sau" (each the `aria-label` and the tooltip of its
  button), `.nav`, `.today` and `.selected` (existing), and `ui.calendar.monthDropdown` "Chọn tháng" and
  `.yearDropdown` "Chọn năm" (*new*, the labels of the two dropdowns, which `components/ui/calendar.tsx` leaves out
  while no screen shows them); the checks of the web (*new*)
  `validation.nameRequired` "Vui lòng nhập họ tên.", `.nameTooLong` "Họ tên tối đa 100 ký tự.",
  `.employeeCodeRequired` "Vui lòng nhập mã nhân viên.", `.employeeCodeTooLong` "Mã nhân viên tối đa 50 ký tự.",
  `.employeeCodeSpaces` "Mã nhân viên không có khoảng trắng ở đầu hoặc cuối.", `.workPhoneInvalid` "SĐT công việc
  gồm 8 đến 15 chữ số, có thể bắt đầu bằng dấu +.", `.emailRequired` "Vui lòng nhập email công ty.", `.emailInvalid`
  "Email không hợp lệ." (the words of `signIn.validation.emailInvalid`), `.emailTooLong` "Email tối đa 255 ký tự.",
  `.rolesRequired` "Vui lòng chọn ít nhất một vai trò." and `.rolesTooMany` "Chọn tối đa 20 vai trò."; the Empty of no
  role `noRoles.title` "Bạn chưa được phép gán vai trò nào." with `noRoles.description` "Vai trò của bạn chưa được
  giao quản lý vai trò nào, nên chưa tạo được tài khoản. Liên hệ quản trị viên để được cấp." (*new*); the Empty of a
  failed load, titled with the problem's `detail` or the web's text of *Conventions* (then with `rolesError.description`
  "Chưa tải được danh sách vai trò, nên chưa tạo được tài khoản. Những gì bạn đã nhập vẫn được giữ.", *new*, since
  the web's own text says nothing of the form), with `admin.retry`; the buttons `staff.account.cancel` "Hủy" and
  `submit` "Tạo tài khoản" (*new*); the toasts `created` "Đã tạo tài khoản. Nhân viên đăng nhập bằng mật khẩu mặc
  định và phải đổi mật khẩu ngay lần đầu." (*new*) and `createdPending` "Đã tạo tài khoản, đang chờ duyệt. Nhân viên
  đăng nhập được sau khi một người có quyền duyệt tài khoản này." (*new*, STF-09); the notice (STF-09)
  `pendingNotice.title` "Tài khoản bạn tạo sẽ chờ duyệt" and `pendingNotice.description` "Bạn không có quyền duyệt tài
  khoản, nên tài khoản mới ở trạng thái Chờ duyệt. Nhân viên chưa đăng nhập được cho tới khi một người khác có quyền
  duyệt tài khoản này." (*new*).
- **Component tree** (DEC-118 (3)), under `web/src/features/identity/staff-accounts/components/`:

  ```text
  staff-create-layout.tsx                 the Card as the `form` (noValidate), `max-w-2xl mx-auto`; slots: header |
  │                                       notice | problems | profile | account | actions; narrow: the page's 16px
  │                                       padding, the roles in one column, the actions stacked
  ├── staff-create-header-section.tsx     CardHeader: CardTitle `staff.list.add` as the page's h1;
  │                                       CardDescription `staff.create.description`, then `admin.requiredNote`
  ├── staff-create-pending-notice.tsx     (STF-09) Alert default with ClockIcon, its role set to `status` in place
  │                                       of shadcn's `role="alert"` (as the notice of SCR-STF-03), AlertTitle
  │                                       `pendingNotice.title`, AlertDescription `pendingNotice.description`; only
  │                                       when `permissions` lack `staff.approve`
  ├── (problems slot)                     `FormProblems` of `components/app/form-problems.tsx`
  ├── staff-create-profile-section.tsx    FieldSet: FieldLegend `profile.title`, FieldDescription
  │                                       `profile.description`; FieldGroup of: Họ tên * (Input `required`,
  │                                       FieldDescription `nameHint`), Mã nhân viên * (Input `required`,
  │                                       `spellCheck=false`, FieldDescription `employeeCodeHint`), Ngày vào làm *,
  │                                       SĐT công việc (Input `type="tel"`, placeholder `workPhonePlaceholder`,
  │                                       FieldDescription `workPhoneHint`); each FieldError under its input, before
  │                                       the hint; the ` *` a span `aria-hidden` inside FieldLabel
  │   └── staff-joined-date-field.tsx     presentational — Popover with Calendar (date-fns, locale `vi`, Monday first,
  │                                       today marked, `captionLayout="dropdown"`, 50 years back to 1 year ahead)
  │                                       behind a Button `outline` w-60 (full width narrow; never empty, so no
  │                                       `aria-required`)
  │                                       showing `dd/MM/yyyy` beside CalendarIcon; `required`; FieldDescription
  │                                       `joinedDateHint`; the month buttons in the calendar's own Tooltips
  ├── staff-create-account-section.tsx    FieldSeparator, then FieldSet: FieldLegend `account.title`, FieldDescription
  │                                       `account.description`; FieldGroup of: Email công ty * (Input
  │                                       `type="email"`, `required`, `spellCheck=false`, placeholder
  │                                       `emailPlaceholder`, FieldDescription `emailHint`), Vai trò *
  │   └── staff-create-roles-field.tsx    presentational — FieldSet (its hint says it is required) with FieldLegend
  │                                       `staff.list.columns.roles` and its ` *` (variant label), FieldDescription
  │                                       `rolesHint`, FieldError, and a grid of Field orientation horizontal with
  │                                       Checkbox and FieldLabel per role (`grid-cols-1 sm:grid-cols-2`); loading:
  │                                       six rows of Skeleton (`aria-busy`); empty: Empty outline with ShieldXIcon,
  │                                       EmptyTitle `noRoles.title`, EmptyDescription `noRoles.description`; error:
  │                                       Empty outline with TriangleAlertIcon, EmptyTitle the problem's text
  │                                       (destructive, `role="alert"`), EmptyDescription `rolesError.description`
  │                                       only under the web's own text, EmptyContent Button `outline`
  │                                       `admin.retry` disabled while the retry is pending (the failed load of
  │                                       SCR-ADM-01 and SCR-STF-03)
  └── staff-create-actions-section.tsx    CardFooter with border-t, `justify-end`: Button `outline` asChild Link
                                          `staff.account.cancel` to SCR-STF-01; Button `default` type submit
                                          `submit`, disabled while the roles are not ready and while sending
                                          (LoaderCircleIcon `motion-safe:animate-spin`); narrow: `flex-col-reverse`,
                                          full width
  ../hooks/use-assignable-roles.ts        the generated query of `GET /api/v1/admin/staff/assignable-roles`, every
                                          page (`pageSize` 100)
  ../hooks/use-staff-create-form.ts       the form with `staffCreateSchema` of `../schemas.ts`; the mutation of
                                          `POST /api/v1/admin/staff`; puts each `errors` item under the field its
                                          `pointer` names (`#/roleIds` and `#/roleIds/<n>` under Vai trò), the rest
                                          in FormProblems; on 201 invalidates the staff list and the count waiting,
                                          then routes to SCR-STF-03 with the toast of the answer's `status`
  ```
